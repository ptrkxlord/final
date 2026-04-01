using System;
using System.IO;
using System.Runtime.InteropServices;

namespace EmoCore.Tools
{
    public class ResourceCloner
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, uint dwFlags);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr FindResource(IntPtr hModule, IntPtr lpName, IntPtr lpType);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr LoadResource(IntPtr hModule, IntPtr hResource);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr LockResource(IntPtr hResData);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern uint SizeofResource(IntPtr hModule, IntPtr hResource);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr BeginUpdateResource(string pFileName, bool bDeleteExistingResources);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool UpdateResource(IntPtr hUpdate, IntPtr lpType, IntPtr lpName, ushort wLanguage, IntPtr lpData, uint cbData);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool EndUpdateResource(IntPtr hUpdate, bool fDiscard);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool FreeLibrary(IntPtr hModule);

        const uint LOAD_LIBRARY_AS_DATAFILE = 0x00000002;
        static readonly IntPtr RT_VERSION = (IntPtr)16;
        static readonly IntPtr RT_GROUP_ICON = (IntPtr)14;
        static readonly IntPtr RT_ICON = (IntPtr)3;

        public static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: ResourceCloner.exe <donor_exe> <target_exe>");
                return;
            }

            string donor = args[0];
            string target = args[1];

            if (!File.Exists(donor)) { Console.WriteLine("Donor not found."); return; }
            if (!File.Exists(target)) { Console.WriteLine("Target not found."); return; }

            Console.WriteLine($"[*] Cloning resources from {donor} to {target}...");

            IntPtr hModule = LoadLibraryEx(donor, IntPtr.Zero, LOAD_LIBRARY_AS_DATAFILE);
            if (hModule == IntPtr.Zero) { Console.WriteLine("Failed to load donor."); return; }

            IntPtr hUpdate = BeginUpdateResource(target, false);
            if (hUpdate == IntPtr.Zero) { Console.WriteLine("Failed to open target for update."); return; }

            // Clone Version
            CloneResource(hModule, hUpdate, RT_VERSION, (IntPtr)1);
            
            // Clone Icons
            CloneResource(hModule, hUpdate, RT_GROUP_ICON, (IntPtr)1);
            // Note: RT_ICONs are usually many, for svchost we just care about the main ones
            for (int i = 1; i <= 20; i++) 
                CloneResource(hModule, hUpdate, RT_ICON, (IntPtr)i);

            if (EndUpdateResource(hUpdate, false))
                Console.WriteLine("[+] Resources cloned successfully.");
            else
                Console.WriteLine("[!] Failed to finalize resource update.");

            FreeLibrary(hModule);
        }

        static void CloneResource(IntPtr hModule, IntPtr hUpdate, IntPtr type, IntPtr name)
        {
            IntPtr hRes = FindResource(hModule, name, type);
            if (hRes == IntPtr.Zero) return;

            uint size = SizeofResource(hModule, hRes);
            IntPtr hResData = LoadResource(hModule, hRes);
            IntPtr pResData = LockResource(hResData);

            if (pResData != IntPtr.Zero)
            {
                UpdateResource(hUpdate, type, name, 1033, pResData, size);
            }
        }
    }
}
