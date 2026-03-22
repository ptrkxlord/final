using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace FinalBot.Modules
{
    public static class ScreenshotModule
    {
        public static string TakeScreenshot(string outputDir)
        {
            try 
            {
                string filePath = Path.Combine(outputDir, $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.jpg");
                
                Rectangle bounds = Screen.PrimaryScreen.Bounds;
                using (Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height))
                {
                    using (Graphics g = Graphics.FromImage(bitmap))
                    {
                        g.CopyFromScreen(Point.Empty, Point.Empty, bounds.Size);
                    }
                    bitmap.Save(filePath, ImageFormat.Jpeg);
                }
                
                return filePath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SCREENSHOT ERROR] {ex.Message}");
                return null;
            }
        }
    }
}
