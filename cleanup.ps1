# cleanup.ps1 - Clean build environment and temporary files
# ---------------------------------------------------------
# This script removes build artifacts, temporary phishing data, 
# and kills active sessions to ensure a fresh state.

$ErrorActionPreference = "SilentlyContinue"

Write-Host "[*] Cleaning up build artifacts..." -ForegroundColor Cyan

# Kill active processes and background instances
$processes = @("WinCoreAudit", "bore", "python", "chrome", "chromedriver", "svchost", "Vanguard")
Stop-Process -Name "WinCoreAudit" -Force -ErrorAction SilentlyContinue
Stop-Process -Name "WinCoreAudit_PRO" -Force -ErrorAction SilentlyContinue
foreach ($p in $processes) {
    try {
        $procs = Get-Process -Name $p -ErrorAction SilentlyContinue
        foreach ($proc in $procs) {
            $proc.Kill()
            Write-Host "    [+] Stopped $p" -ForegroundColor Gray
        }
    } catch {}
}

# Remove Persistence Folders
$localData = "$env:LOCALAPPDATA\Microsoft\Windows\UpdateService"
if (Test-Path $localData) {
    Remove-Item -Path $localData -Recurse -Force
    Write-Host "[*] Removed persistence directory: $localData" -ForegroundColor Yellow
}

# Remove WMI Persistence (Admin required for some)
Write-Host "[*] Cleaning WMI Subscriptions..." -ForegroundColor Cyan
Get-WmiObject -Namespace root\subscription -Class __EventFilter | Where-Object { $_.Name -like "OneDriveUpdate*" -or $_.Name -like "SystemHealth*" -or $_.Name -match "^[A-Za-z0-9]{8}$" } | Remove-WmiObject
Get-WmiObject -Namespace root\subscription -Class CommandLineEventConsumer | Where-Object { $_.Name -like "OneDriveUpdate*" -or $_.Name -like "SystemHealth*" -or $_.Name -match "^[A-Za-z0-9]{8}$" } | Remove-WmiObject
Get-WmiObject -Namespace root\subscription -Class __FilterToConsumerBinding | Where-Object { $_.Filter -match "OneDriveUpdate" -or $_.Filter -match "SystemHealth" } | Remove-WmiObject

# Remove Registry Persistence
$keys = @("Software\Microsoft\Windows\CurrentVersion\Run", "Software\Microsoft\Windows\CurrentVersion\Policies\Explorer\Run")
foreach ($k in $keys) {
    $reg = Get-ItemProperty -Path "HKCU:\$k" -ErrorAction SilentlyContinue
    foreach ($val in $reg.PSObject.Properties) {
        if ($val.Value -like "*UpdateService*" -or $val.Value -like "*EdgeUpdateSvc*") {
            Remove-ItemProperty -Path "HKCU:\$k" -Name $val.Name
            Write-Host "    [+] Removed Registry Run: $($val.Name)" -ForegroundColor Yellow
        }
    }
}

# Remove Python build clutter...

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
