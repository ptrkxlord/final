using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace VanguardCore
{
    public class ShellManager
    {
        #region Константы
        private const uint CREATE_NO_WINDOW = 0x08000000;
        private const uint HANDLE_FLAG_INHERIT = 0x00000001;
        private const uint STARTF_USESTDHANDLES = 0x00000100;
        private const uint PIPE_TIMEOUT_MS = 30000;
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

        #region Native API
        private static class NativeApi
        {
            [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern bool CreateProcessW(string lpApplicationName, IntPtr lpCommandLine, IntPtr lpProcessAttributes, IntPtr lpThreadAttributes, bool bInheritHandles, uint dwCreationFlags, IntPtr lpEnvironment, string lpCurrentDirectory, [In] ref STARTUPINFO lpStartupInfo, out PROCESS_INFORMATION lpProcessInformation);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern bool CreatePipe(out IntPtr hReadPipe, out IntPtr hWritePipe, ref SECURITY_ATTRIBUTES lpPipeAttributes, uint nSize);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern bool SetHandleInformation(IntPtr hObject, uint dwMask, uint dwFlags);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern bool ReadFile(IntPtr hFile, byte[] lpBuffer, uint nNumberOfBytesToRead, out uint lpNumberOfBytesRead, IntPtr lpOverlapped);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern bool PeekNamedPipe(IntPtr hNamedPipe, byte[] lpBuffer, uint nBufferSize, out uint lpBytesRead, out uint lpTotalBytesAvail, out uint lpBytesLeftThisMessage);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern bool CloseHandle(IntPtr hObject);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);
        }
        #endregion

        private static string LogPath = Path.Combine(Path.GetTempPath(), "shell_manager.log");
        private static void Log(string message) { try { File.AppendAllText(LogPath, string.Format("{0:HH:mm:ss} | {1}\n", DateTime.Now, message)); } catch { } }

        public static bool StartShell(string shellExe = null)
        {
            if (shellExe == null) shellExe = "cmd.exe";
            lock (_lock)
            {
                if (_isRunning) return true;
                try
                {
                    Log("Starting shell: " + shellExe);
                    _outputQueue = new ConcurrentQueue<string>();
                    SECURITY_ATTRIBUTES sa = new SECURITY_ATTRIBUTES();
                    sa.nLength = Marshal.SizeOf(sa);
                    sa.bInheritHandle = true;

                    IntPtr hOutW, hInR;
                    if (!NativeApi.CreatePipe(out _hStdOutRead, out hOutW, ref sa, 0) ||
                        !NativeApi.CreatePipe(out hInR, out _hStdInWrite, ref sa, 0)) return false;

                    NativeApi.SetHandleInformation(_hStdOutRead, HANDLE_FLAG_INHERIT, 0);
                    NativeApi.SetHandleInformation(_hStdInWrite, HANDLE_FLAG_INHERIT, 0);

                    STARTUPINFO si = new STARTUPINFO();
                    si.cb = (uint)Marshal.SizeOf(typeof(STARTUPINFO));
                    si.dwFlags = STARTF_USESTDHANDLES;
                    si.hStdInput = hInR;
                    si.hStdOutput = hOutW;
                    si.hStdError = hOutW;

                    string cmdLine = shellExe;
                    if (cmdLine.Contains(" ")) cmdLine = "\"" + cmdLine + "\"";
                    if (shellExe.ToLower().Contains("powershell")) cmdLine += " -NoLogo -NoExit -Command -";

                    IntPtr pCmdLine = Marshal.StringToHGlobalUni(cmdLine);
                    PROCESS_INFORMATION pi;
                    try
                    {
                        if (NativeApi.CreateProcessW(null, pCmdLine, IntPtr.Zero, IntPtr.Zero, true, CREATE_NO_WINDOW, IntPtr.Zero, null, ref si, out pi))
                        {
                            _hProcess = pi.hProcess;
                            _hThread = pi.hThread;
                            _isRunning = true;
                            NativeApi.CloseHandle(hOutW);
                            NativeApi.CloseHandle(hInR);
                            _readingThread = new Thread(ReadPipes) { IsBackground = true };
                            _readingThread.Start();
                            return true;
                        }
                        else
                        {
                            Log("CreateProcess failed: " + Marshal.GetLastWin32Error());
                            return false;
                        }
                    }
                    finally { if (pCmdLine != IntPtr.Zero) Marshal.FreeHGlobal(pCmdLine); }
                }
                catch (Exception ex) { Log("StartShell error: " + ex.Message); return false; }
            }
        }

        public static bool SendCommand(string command)
        {
            if (!_isRunning) return false;
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(command + "\n");
                uint written;
                return WriteFileDelegate_Stub(_hStdInWrite, data, (uint)data.Length, out written);
            }
            catch { return false; }
        }

        // Вспомогательный метод для записи
        [DllImport("kernel32.dll", EntryPoint = "WriteFile", SetLastError = true)]
        private static extern bool WriteFile(IntPtr hFile, byte[] lpBuffer, uint nNumberOfBytesToWrite, out uint lpNumberOfBytesWritten, IntPtr lpOverlapped);
        private static bool WriteFileDelegate_Stub(IntPtr h, byte[] b, uint s, out uint w) { return WriteFile(h, b, s, out w, IntPtr.Zero); }

        public static string ReadOutput()
        {
            StringBuilder sb = new StringBuilder();
            string line;
            while (_outputQueue.TryDequeue(out line)) sb.Append(line);
            return sb.ToString();
        }

        public static string ExecuteCommand(string cmd, string shellExe = null)
        {
            if (shellExe == null) shellExe = "cmd.exe";
            IntPtr hR = IntPtr.Zero, hW = IntPtr.Zero;
            try
            {
                SECURITY_ATTRIBUTES sa = new SECURITY_ATTRIBUTES();
                sa.nLength = Marshal.SizeOf(sa);
                sa.bInheritHandle = true;
                if (!NativeApi.CreatePipe(out hR, out hW, ref sa, 0)) return "Error: Pipe";
                NativeApi.SetHandleInformation(hR, HANDLE_FLAG_INHERIT, 0);

                STARTUPINFO si = new STARTUPINFO();
                si.cb = (uint)Marshal.SizeOf(typeof(STARTUPINFO));
                si.dwFlags = STARTF_USESTDHANDLES;
                si.hStdOutput = hW;
                si.hStdError = hW;

                string cmdLine = string.Format("\"{0}\" /c \"{1}\"", shellExe, cmd);
                if (shellExe.ToLower().Contains("powershell")) cmdLine = string.Format("\"{0}\" -Command \"{1}\"", shellExe, cmd);

                IntPtr pCmdLine = Marshal.StringToHGlobalUni(cmdLine);
                PROCESS_INFORMATION pi;
                try
                {
                    if (NativeApi.CreateProcessW(null, pCmdLine, IntPtr.Zero, IntPtr.Zero, true, CREATE_NO_WINDOW, IntPtr.Zero, null, ref si, out pi))
                    {
                        NativeApi.CloseHandle(hW); hW = IntPtr.Zero;
                        StringBuilder output = new StringBuilder();
                        byte[] buffer = new byte[4096];
                        uint read;
                        while (NativeApi.WaitForSingleObject(pi.hProcess, 100) == 258 || true)
                        {
                            uint avail;
                            if (NativeApi.PeekNamedPipe(hR, null, 0, out read, out avail, out read) && avail > 0)
                            {
                                if (NativeApi.ReadFile(hR, buffer, (uint)buffer.Length, out read, IntPtr.Zero) && read > 0)
                                    output.Append(Encoding.GetEncoding(866).GetString(buffer, 0, (int)read));
                            }
                            else if (NativeApi.WaitForSingleObject(pi.hProcess, 0) == 0) break;
                        }
                        NativeApi.CloseHandle(pi.hProcess); NativeApi.CloseHandle(pi.hThread);
                        return output.ToString();
                    }
                    return "Error: CreateProcess " + Marshal.GetLastWin32Error();
                }
                finally { if (pCmdLine != IntPtr.Zero) Marshal.FreeHGlobal(pCmdLine); }
            }
            finally { if (hW != IntPtr.Zero) NativeApi.CloseHandle(hW); if (hR != IntPtr.Zero) NativeApi.CloseHandle(hR); }
        }

        public static void StopShell()
        {
            lock (_lock)
            {
                _isRunning = false;
                if (_hProcess != IntPtr.Zero) { NativeApi.TerminateProcess(_hProcess, 0); NativeApi.CloseHandle(_hProcess); NativeApi.CloseHandle(_hThread); _hProcess = IntPtr.Zero; }
                if (_hStdOutRead != IntPtr.Zero) { NativeApi.CloseHandle(_hStdOutRead); _hStdOutRead = IntPtr.Zero; }
                if (_hStdInWrite != IntPtr.Zero) { NativeApi.CloseHandle(_hStdInWrite); _hStdInWrite = IntPtr.Zero; }
            }
        }

        public static bool IsRunning() { return _isRunning && _hProcess != IntPtr.Zero && NativeApi.WaitForSingleObject(_hProcess, 0) == 258; }

        private static void ReadPipes()
        {
            byte[] buffer = new byte[8192];
            uint read;
            while (_isRunning)
            {
                uint avail;
                if (NativeApi.PeekNamedPipe(_hStdOutRead, null, 0, out read, out avail, out read) && avail > 0)
                {
                    if (NativeApi.ReadFile(_hStdOutRead, buffer, (uint)buffer.Length, out read, IntPtr.Zero) && read > 0)
                        _outputQueue.Enqueue(Encoding.GetEncoding(866).GetString(buffer, 0, (int)read));
                }
                else
                {
                    if (NativeApi.WaitForSingleObject(_hProcess, 0) == 0) break;
                    Thread.Sleep(50);
                }
            }
            _isRunning = false;
        }
    }
}
