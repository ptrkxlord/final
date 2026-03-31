using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace VanguardBuilder
{
    class ResourcePacker
    {
        // Format: [Nonce: 12][Tag: 16][Ciphertext: N]
        static void Main(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("Usage: ResourcePacker <input_file> <output_file> <session_key_b64>");
                return;
            }

            try
            {
                string inputPath = args[0];
                string outputPath = args[1];
                byte[] sessionKey = Convert.FromBase64String(args[2]);

                if (!File.Exists(inputPath)) {
                    Console.WriteLine($"[!] Input file not found: {inputPath}");
                    return;
                }

                byte[] rawData = File.ReadAllBytes(inputPath);
                
                // 1. Compress with GZip
                byte[] compressedData;
                using (MemoryStream ms = new MemoryStream())
                {
                    using (GZipStream gzip = new GZipStream(ms, CompressionLevel.Optimal))
                    {
                        gzip.Write(rawData, 0, rawData.Length);
                    }
                    compressedData = ms.ToArray();
                }

                // 2. Encrypt with AES-GCM
                byte[] nonce = new byte[12];
                RandomNumberGenerator.Fill(nonce);
                
                byte[] tag = new byte[16];
                byte[] ciphertext = new byte[compressedData.Length];

                // V7.2 Fix: Use the correct constructor to avoid SYSLIB0053
                using (AesGcm aesGcm = new AesGcm(sessionKey, 16))
                {
                    aesGcm.Encrypt(nonce, compressedData, ciphertext, tag);
                }

                // 3. Final Format: Nonce + Tag + Ciphertext
                byte[] finalData = new byte[12 + 16 + ciphertext.Length];
                Buffer.BlockCopy(nonce, 0, finalData, 0, 12);
                Buffer.BlockCopy(tag, 0, finalData, 12, 16);
                Buffer.BlockCopy(ciphertext, 0, finalData, 28, ciphertext.Length);

                File.WriteAllBytes(outputPath, finalData);
                
                double ratio = (double)finalData.Length / rawData.Length * 100;
                Console.WriteLine($"[+] Packed {Path.GetFileName(inputPath)}: {rawData.Length / 1024}KB -> {finalData.Length / 1024}KB ({ratio:F1}%)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[!] Error: {ex.Message}");
                Environment.Exit(1);
            }
        }
    }
}
