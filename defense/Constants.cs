using System;

namespace VanguardCore
{
    public static class Constants
    {
        // This key is updated at build time by full_rebuild.ps1
        public const byte RESOURCE_XOR_KEY = 0x52; 
        
        // Target process name for PPID spoofing
        public const string SPOOF_PARENT = "explorer";
        
        // Stealth Monikers
        public const string MONIKER_PREFIX = "Elevation:Administrator!new:";
    }
}
