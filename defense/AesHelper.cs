using System;
using System.IO;
using System.Security.Cryptography;
using VanguardCore;

namespace VanguardCore
{
    public static class AesHelper
    {
        public static byte[] Decrypt(byte[] encryptedData)
        {
            if (encryptedData == null || encryptedData.Length == 0) return null;

            try
            {
                byte[] key = Convert.FromBase64String(Constants.RESOURCE_AES_KEY);
                byte[] iv = Convert.FromBase64String(Constants.RESOURCE_AES_IV);

                using (Aes aes = Aes.Create())
                {
                    aes.Key = key;
                    aes.IV = iv;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;

                    using (ICryptoTransform decryptor = aes.CreateDecryptor())
                    {
                        return decryptor.TransformFinalBlock(encryptedData, 0, encryptedData.Length);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AES_DECRYPT_ERROR] {ex.Message}");
                return null;
            }
        }
    }
}
