$files = Get-ChildItem -Recurse -Filter *.cs
foreach ($file in $files) {
    if ($file.FullName -match "scripts\\Program.cs" -or $file.FullName -match "defense\\SafetyManager.cs" -or $file.FullName -match "defense\\StringVault.cs") { continue }
    
    $lines = Get-Content $file.FullName
    $balance = 0
    $newLines = @()
    $fixed = $false

    foreach ($line in $lines) {
        $open = ($line.ToCharArray() | Where-Object {$_ -eq '{'}).Count
        $close = ($line.ToCharArray() | Where-Object {$_ -eq '}'}).Count
        
        # We only start counting balance once we see the first brace (namespace or class)
        # to avoid issues with using blocks or attributes if any.
        $balance += $open
        $balance -= $close
        
        $newLines += $line
        
        if ($balance -eq 0 -and ($open -gt 0 -or $close -gt 0)) {
            # We reached a balanced state at the end of a block.
            # In these files, that should be the end of the namespace.
            $fixed = $true
            break
        }
    }

    if ($fixed -and $newLines.Count -lt $lines.Count) {
        Write-Host "Fixed: $($file.FullName) (Truncated from $($lines.Count) to $($newLines.Count) lines)"
        $newLines | Out-File $file.FullName -Encoding UTF8
    }
}
Write-Host "Structural Restoration Complete."
