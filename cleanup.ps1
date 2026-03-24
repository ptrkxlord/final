# cleanup.ps1 - Clean build environment and temporary files
# ---------------------------------------------------------
# This script removes build artifacts, temporary phishing data, 
# and kills active sessions to ensure a fresh state.

$ErrorActionPreference = "SilentlyContinue"

Write-Host "[*] Cleaning up build artifacts..." -ForegroundColor Cyan

# Kill active processes
$processes = @("EdgeUpdateSvc", "SteamLogin", "SteamAlert")
foreach ($p in $processes) {
    try {
        Stop-Process -Name $p -Force -ErrorAction SilentlyContinue
        Write-Host "    [+] Stopped $p" -ForegroundColor Gray
    } catch {}
}

# Remove Python build clutter
$buildDirs = @("build", "dist", "main_ui.build", "main_ui.dist", "main_ui.onefile-build", "steam_notice.build", "steam_notice.dist", "steam_notice.onefile-build")
foreach ($dir in $buildDirs) {
    if (Test-Path $dir) {
        Remove-Item -Path $dir -Recurse -Force
        Write-Host "    [+] Removed $dir" -ForegroundColor Gray
    }
}

# Remove spec files
Remove-Item -Path "*.spec" -Force

# Remove encrypted binaries and compiled EXEs in root
$binaries = @("SteamAlert.exe", "SteamLogin.exe", "SteamAlert.bin", "SteamLogin.bin", "chromelevator.bin")
foreach ($bin in $binaries) {
    if (Test-Path $bin) {
        Remove-Item -Path $bin -Force
        Write-Host "    [+] Removed $bin" -ForegroundColor Gray
    }
}

# Clear temporary phish data (logs/cookies)
$tempDir = "$env:TEMP\FinalTempSys\tablichka"
if (Test-Path $tempDir) {
    Remove-Item -Path "$tempDir\*" -Force
    Write-Host "[!] Cleared temporary phishing settings/logs in $tempDir" -ForegroundColor Yellow
}

Write-Host "`n[SUCCESS] Cleanup complete!" -ForegroundColor Green
