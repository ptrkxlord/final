# CleanUp Bot Traces
$ErrorActionPreference = "SilentlyContinue"

Write-Host "[*] Starting deep cleanup..." -ForegroundColor Cyan

# 1. Kill bot processes
Write-Host "[*] Terminating bot processes..." -ForegroundColor Yellow
$targetDir = "$env:LOCALAPPDATA\Microsoft\Windows\UpdateService"
Get-Process svchost, FinalBot | Where-Object { 
    try { $_.Path -like "*$targetDir*" -or $_.Path -like "*Desktop\final*" } catch { $false }
} | Stop-Process -Force

# 2. Remove WMI persistence
Write-Host "[*] Removing WMI Event Subscriptions..." -ForegroundColor Yellow
$wmiFilters = Get-WMIObject -Namespace root\subscription -Class __EventFilter
foreach ($filter in $wmiFilters) {
    if ($filter.Query -like "*explorer.exe*" -and ($filter.Name.Length -eq 13 -or $filter.Name -match "^Update")) {
        Write-Host "[-] Deleting WMI Filter: $($filter.Name)"
        $filter.Delete()
    }
}

$wmiConsumers = Get-WMIObject -Namespace root\subscription -Class CommandLineEventConsumer
foreach ($consumer in $wmiConsumers) {
    if ($consumer.CommandLineTemplate -like "*UpdateService*") {
        Write-Host "[-] Deleting WMI Consumer: $($consumer.Name)"
        $consumer.Delete()
    }
}

# 3. Clean up Registry
Write-Host "[*] Cleaning Registry keys..." -ForegroundColor Yellow
Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "SecurityHealthSvcHost" -Force
Remove-ItemProperty -Path "HKCU:\Environment" -Name "UserInitMprLogonScript" -Force

# 4. Remove Firewall Rule
Write-Host "[*] Removing Firewall rules..." -ForegroundColor Yellow
Remove-NetFirewallRule -DisplayName "Windows Update Service" -ErrorAction SilentlyContinue

# 5. Delete Files
Write-Host "[*] Deleting persistence files..." -ForegroundColor Yellow
if (Test-Path $targetDir) {
    Remove-Item $targetDir -Recurse -Force
    Write-Host "[-] Removed: $targetDir"
}

# 6. Remove Debug Logs
Remove-Item "C:\Users\Public\svchost_debug.log" -Force

Write-Host "[+] Cleanup completed successfully!" -ForegroundColor Green
