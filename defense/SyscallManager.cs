using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Text;

namespace VanguardCore
{
    public static class SyscallManager
    {
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
            foreach (char c in s)
                hash = ((hash << 5) + hash) + (uint)c;
            return hash;
        }

        public static void Initialize()
        {
            if (_initialized) return;
            try
            {
                IntPtr ntdll = GetModuleHandle("ntdll.dll");
                if (ntdll == IntPtr.Zero) return;

                // Pre-warm critical syscalls
                string[] criticalTable = {
                    "NtAllocateVirtualMemory", "NtWriteVirtualMemory", 
                    "NtProtectVirtualMemory", "NtCreateThreadEx",
                    "NtTerminateProcess", "NtGetNextProcess"
                };

                foreach (string name in criticalTable)
                {
                    uint hash = DJB2(name);
                    SYSCALL_ENTRY entry = FindSyscall(ntdll, name);
                    if (entry.pAddress != IntPtr.Zero)
                        _syscallCache[hash] = entry;
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
                // Simple version: find the function address and parse the SSN from the stub
                // A standard syscall stub looks like:
                // mov r10, rcx
                // mov eax, <SSN>
                // syscall
                // ret
                
                IntPtr pFunc = GetProcAddress(hModule, functionName);
                if (pFunc == IntPtr.Zero) return entry;

                // Check for 'mov eax, SSN' pattern: 0xB8 followed by 4 byte SSN
                // Bytes: 4C 8B D1 B8 <SSN_LOW> <SSN_HIGH> 00 00
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

        // Direct Syscall Execution using Delegate and Function Pointer
        // On x64, we can use a small shellcode to execute the syscall
        private static byte[] _syscallStub = {
            0x4C, 0x8B, 0xD1,               // mov r10, rcx
            0xB8, 0x00, 0x00, 0x00, 0x00,   // mov eax, <SSN>
            0x0F, 0x05,                     // syscall
            0xC3                            // ret
        };

        public static uint ExecuteSyscall(uint hash, params object[] args)
        {
            if (!_syscallCache.ContainsKey(hash)) return 0xC0000001; // STATUS_UNSUCCESSFUL

            SYSCALL_ENTRY entry = _syscallCache[hash];
            
            // Note: In a real production environment, we'd use a more sophisticated 
            // way to invoke the syscall (like dynamic shellcode or D/Invoke).
            // For now, we simulate the logic.
            return 0; 
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);
    }
}
