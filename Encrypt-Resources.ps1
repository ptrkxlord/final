$files = @("chromelevator.exe", "SteamAlert.exe", "SteamLogin.exe")
$key = 0xAA

foreach ($file in $files) {
    if (Test-Path $file) {
        $bytes = [IO.File]::ReadAllBytes("$PWD\$file")
        for ($i = 0; $i -lt $bytes.Length; $i++) {
            $bytes[$i] = $bytes[$i] -bxor $key
        }
        $outFile = $file.Replace(".exe", ".bin")
        [IO.File]::WriteAllBytes("$PWD\$outFile", $bytes)
        Write-Host "Encrypted $file to $outFile"
    } else {
        Write-Warning "File $file not found!"
    }
}
