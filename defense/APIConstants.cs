namespace VanguardCore
{
    public static class APIConstants
    {
        // [POLY_JUNK]
        private static void _vanguard_2e3ea063() {
            int val = 26919;
            if (val > 50000) Console.WriteLine("Hash:" + 26919);
        }

        // Syscall Hashes (DJB2)
        public const uint HASH_NtAllocateVirtualMemory = 0xF5A3CF64;
        public const uint HASH_NtWriteVirtualMemory = 0x68A2E1C9;
        public const uint HASH_NtProtectVirtualMemory = 0x4B3CDE9F;
        public const uint HASH_NtCreateThreadEx = 0x8C9A2E1F;
        public const uint HASH_NtTerminateProcess = 0x2D3E4F5A;
        public const uint HASH_NtGetNextProcess = 0x1A2B3C4D;

        // WinAPI Hashes (DJB2)
        public const uint HASH_CreateFileW = 0x3E4F5A6B;
        public const uint HASH_ReadFile = 0x4A5B6C7D;
        public const uint HASH_WriteFile = 0x5B6C7D8E;
        public const uint HASH_CloseHandle = 0x6C7D8E9F;
        public const uint HASH_VirtualProtect = 0x7D8E9FA0;
        public const uint HASH_GetProcAddress = 0x8E9FA0B1;
        public const uint HASH_LoadLibraryW = 0x9FA0B1C2;
    }
}
