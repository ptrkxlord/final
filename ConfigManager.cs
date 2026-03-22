using System;
using System.Collections.Generic;
using VanguardCore;

namespace FinalBot
{
    public static class ConfigManager
    {
        private static Dictionary<string, string> _config = new Dictionary<string, string>();

        public static void Load()
        {
            // Load base secrets from SafetyManager
            _config["BOT_TOKEN"] = SafetyManager.GetSecret("BOT_TOKEN");
            _config["ADMIN_ID"] = SafetyManager.GetSecret("ADMIN_ID");
            _config["C2_URL"] = SafetyManager.GetSecret("GIST_URL");
            _config["GIST_GITHUB_TOKEN"] = SafetyManager.GetSecret("GIST_GITHUB_TOKEN");
            
            Console.WriteLine("[CONFIG] Application configuration loaded.");
        }

        public static string Get(string key, string defaultValue = "")
        {
            if (_config.TryGetValue(key, out string? value))
                return value ?? defaultValue;
            return defaultValue;
        }

        public static long GetLong(string key, long defaultValue = 0)
        {
            if (_config.TryGetValue(key, out string? value) && long.TryParse(value, out long result))
                return result;
            return defaultValue;
        }
    }
}
