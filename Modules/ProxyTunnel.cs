using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Diagnostics;
using VanguardCore;

namespace VanguardCore.Modules
{
    public static class ProxyTunnel
    {
        private static string _currentProxy = null;

        public static async Task<HttpClient> GetBestHttpClient()
        {
            FinalBot.Logger.Info("[PROXY] Connectivity check (China Bypass)...");
            // 1. Try direct connection
            if (await TestTelegramConnectivity(null))
            {
                FinalBot.Logger.Info("[PROXY] Direct connection OK.");
                return new HttpClient();
            }

            // 2. Try proxy from Gist
            FinalBot.Logger.Warn("[PROXY] Direct connection failed. Fetching proxy from Gist...");
            string gistProxy = await GistManager.GetFileContent("proxies.json");
            
            if (!string.IsNullOrEmpty(gistProxy))
            {
                // Ensure protocol prefix
                if (!gistProxy.Contains("://")) gistProxy = "socks5://" + gistProxy;
                
                FinalBot.Logger.Info($"[PROXY] Testing Gist Proxy: {gistProxy}");
                if (await TestTelegramConnectivity(gistProxy))
                {
                    FinalBot.Logger.Info("[PROXY] Gist Proxy is WORKING.");
                    _currentProxy = gistProxy;
                    return CreateProxiedClient(gistProxy);
                }
            }

            FinalBot.Logger.Error("[PROXY] All connectivity attempts failed.");
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
