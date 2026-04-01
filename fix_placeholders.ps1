$path = "defense\SafetyManager.cs"
$content = Get-Content $path -Raw

# 1. Restore VAULT_KEY placeholder
$content = [System.Text.RegularExpressions.Regex]::Replace($content, 'private static byte\[\] VAULT_KEY = Convert\.FromBase64String\(.*?\);', 'private static byte[]? VAULT_KEY;')

# 2. Restore VAULT_IV placeholder
$content = [System.Text.RegularExpressions.Regex]::Replace($content, 'private static byte\[\] VAULT_IV = Convert\.FromBase64String\(.*?\);', 'private static byte[]? VAULT_IV;')

# 3. Restore _vault dictionary placeholder
$VaultPattern = 'private static readonly Dictionary<string, VaultEntry> _vault = new Dictionary<string, VaultEntry>\s*\{[\s\S]*?\};'
$VaultPlaceholder = "private static readonly Dictionary<string, VaultEntry> _vault = new Dictionary<string, VaultEntry>`n        {`n            { `"PLACEHOLDER`", new VaultEntry { C = `"`", T = `"`" } }`n        };"
$content = [System.Text.RegularExpressions.Regex]::Replace($content, $VaultPattern, $VaultPlaceholder)

Set-Content $path $content -Encoding UTF8
Write-Host "[+] SafetyManager.cs placeholders successfully restored."
