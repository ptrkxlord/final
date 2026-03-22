using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace FinalBot.Modules
{
    public static class DiscordRemoteManager
    {
        private static readonly string _tempDir = Path.Combine(Path.GetTempPath(), "FinalTempSys");

        public static string LaunchDiscordBot(string token, string url, string action = "join")
        {
            try
            {
                if (!Directory.Exists(_tempDir)) Directory.CreateDirectory(_tempDir);
                string outPath = Path.Combine(_tempDir, "discord_bot.py");

                // Extract resource
                if (!File.Exists(outPath) || new FileInfo(outPath).Length < 100)
                {
                    var assembly = Assembly.GetExecutingAssembly();
                    using (Stream stream = assembly.GetManifestResourceStream("FinalBot.websocket.discord_bot.py"))
                    {
                        if (stream == null) return "❌ Failed: Resource 'discord_bot.py' not found.";
                        using (FileStream fileStream = new FileStream(outPath, FileMode.Create, FileAccess.Write))
                        {
                            stream.CopyTo(fileStream);
                        }
                    }
                }

                // Launch via Python
                Process.Start(new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = $"\"{outPath}\" \"{token}\" \"{url}\" \"{action}\"",
                    UseShellExecute = true,
                    CreateNoWindow = true
                });

                return $"✅ Discord Remote launched: Action `{action}` started in background.";
            }
            catch (Exception ex)
            {
                return $"❌ Error launching Discord bot: {ex.Message}";
            }
        }
    }
}
