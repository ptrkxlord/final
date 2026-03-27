using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Text;

namespace VanguardCore
{
    public static class Resolver
    {
        private static Dictionary<uint, IntPtr> _cache = new Dictionary<uint, IntPtr>();

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        /// <summary>
        /// Simple DJB2 hash for strings
        /// </summary>
        public static uint GetHash(string str)
        {
            uint hash = 5381;
            foreach (char c in str)
            {
                hash = ((hash << 5) + hash) + (uint)c;
            }
            return hash;
        }

        public static IntPtr GetProcByHash(string dll, uint hash)
        {
            if (_cache.TryGetValue(hash, out IntPtr cachedPtr)) return cachedPtr;

            IntPtr hModule = GetModuleHandle(dll);
            if (hModule == IntPtr.Zero) hModule = LoadLibrary(dll);
            if (hModule == IntPtr.Zero) return IntPtr.Zero;

            // In a truly stealthy version, we would parse the EAT (Export Address Table)
            // For now, we utilize a mini-resolver that searches by string but hides the string in the caller.
            // (The user requested GetProcAddressByHash, which implies parsing EAT).
            
            // Let's implement EAT parsing for maximum professional level.
            IntPtr ptr = GetProcAddressEAT(hModule, hash);
            if (ptr != IntPtr.Zero) _cache[hash] = ptr;
            return ptr;
        }

        private static IntPtr GetProcAddressEAT(IntPtr hModule, uint targetHash)
        {
            try
            {
                long baseAddr = hModule.ToInt64();
                int e_lfanew = Marshal.ReadInt32(hModule, 0x3C);
                long ntHeaders = baseAddr + e_lfanew;
                long optionalHeader = ntHeaders + 0x18;
                
                // x64 check
                short magic = Marshal.ReadInt16((IntPtr)optionalHeader);
                int exportDirOffset = (magic == 0x20b) ? 0x88 : 0x78; // 0x20b is PE32+ (x64)

                int exportDirRva = Marshal.ReadInt32((IntPtr)(ntHeaders + exportDirOffset));
                if (exportDirRva == 0) return IntPtr.Zero;

                IntPtr exportDirPtr = (IntPtr)(baseAddr + exportDirRva);
                int numberOfNames = Marshal.ReadInt32(exportDirPtr, 0x18);
                int functionsRva = Marshal.ReadInt32(exportDirPtr, 0x1C);
                int namesRva = Marshal.ReadInt32(exportDirPtr, 0x20);
                int ordinalsRva = Marshal.ReadInt32(exportDirPtr, 0x24);

                for (int i = 0; i < numberOfNames; i++)
                {
                    int nameRva = Marshal.ReadInt32((IntPtr)(baseAddr + namesRva + i * 4));
                    string name = Marshal.PtrToStringAnsi((IntPtr)(baseAddr + nameRva));
                    if (GetHash(name) == targetHash)
                    {
                        short ordinal = Marshal.ReadInt16((IntPtr)(baseAddr + ordinalsRva + i * 2));
                        int functionRva = Marshal.ReadInt32((IntPtr)(baseAddr + functionsRva + ordinal * 4));
                        return (IntPtr)(baseAddr + functionRva);
                    }
                }
            }
            catch { }
            return IntPtr.Zero;
        }

        public static T GetDelegate<T>(string dll, uint hash) where T : Delegate
        {
            IntPtr ptr = GetProcByHash(dll, hash);
            if (ptr == IntPtr.Zero) return null;
            return Marshal.GetDelegateForFunctionPointer<T>(ptr);
        }
    }
}
