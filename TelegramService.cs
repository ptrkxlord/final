using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace FinalBot
{
    public static class TelegramService
    {
        private static readonly HttpClient _client = new HttpClient();
        private static string? _botToken;
        private static string? _adminId;

        public static void Initialize(string token, string adminId)
        {
            _botToken = token;
            _adminId = adminId;
        }

        public static async Task<bool> SendMessage(string text)
        {
            if (string.IsNullOrEmpty(_botToken) || string.IsNullOrEmpty(_adminId)) return false;

            try
            {
                var url = $"https://api.telegram.org/bot{_botToken}/sendMessage";
                var payload = new
                {
                    chat_id = _adminId,
                    text = text,
                    parse_mode = "Markdown"
                };

                var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
                var response = await _client.PostAsync(url, content);
                
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public static async Task<bool> SendFile(string filePath, string caption = "")
        {
            if (string.IsNullOrEmpty(_botToken) || string.IsNullOrEmpty(_adminId) || !File.Exists(filePath)) 
                return false;

            try
            {
                var url = $"https://api.telegram.org/bot{_botToken}/sendDocument";
                using var form = new MultipartFormDataContent();
                form.Add(new StringContent(_adminId), "chat_id");
                
                if (!string.IsNullOrEmpty(caption))
                    form.Add(new StringContent(caption), "caption");

                var fileContent = new ByteArrayContent(await File.ReadAllBytesAsync(filePath));
                form.Add(fileContent, "document", Path.GetFileName(filePath));

                var response = await _client.PostAsync(url, form);
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }
    }
}
