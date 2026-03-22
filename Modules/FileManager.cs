using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Telegram.Bot.Types.ReplyMarkups;

namespace FinalBot.Modules
{
    public static class FileManager
    {
        private const int _itemsPerPage = 10;

        public static (string Text, InlineKeyboardMarkup Markup) GetDirectoryView(string path, int page = 0)
        {
            try
            {
                if (string.IsNullOrEmpty(path))
                {
                    // Root view (Drives)
                    var drives = DriveInfo.GetDrives().Where(d => d.IsReady).ToList();
                    var driveButtons = new List<InlineKeyboardButton[]>();
                    
                    foreach (var d in drives)
                    {
                        string driveLetter = d.Name.Replace("\\", "");
                        string label = $"💽 {driveLetter} ({d.VolumeLabel})";
                        string data = $"fmd_{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(d.RootDirectory.FullName))}";
                        driveButtons.Add(new[] { InlineKeyboardButton.WithCallbackData(label, data) });
                    }
                    driveButtons.Add(new[] { InlineKeyboardButton.WithCallbackData("⬅️ Back to Admin Panel", "back_to_main") });
                    
                    return ("🖥️ *FILE MANAGER* - Select a Drive", new InlineKeyboardMarkup(driveButtons));
                }

                if (!Directory.Exists(path))
                    return ($"❌ Directory not found: {path}", new InlineKeyboardMarkup(new[] { InlineKeyboardButton.WithCallbackData("⬅️ Back", "file_manager") }));

                // Normal directory view
                var di = new DirectoryInfo(path);
                var dirs = di.GetDirectories().OrderBy(d => d.Name).ToList();
                var files = di.GetFiles().OrderBy(f => f.Name).ToList();

                var allItems = new List<(string Name, string Path, bool IsDir)>();
                allItems.AddRange(dirs.Select(d => (d.Name, d.FullName, true)));
                allItems.AddRange(files.Select(f => (f.Name, f.FullName, false)));

                int totalPages = (int)Math.Ceiling(allItems.Count / (double)_itemsPerPage);
                if (page < 0) page = 0;
                if (page >= totalPages && totalPages > 0) page = totalPages - 1;

                var pagedItems = allItems.Skip(page * _itemsPerPage).Take(_itemsPerPage).ToList();
                
                var buttons = new List<InlineKeyboardButton[]>();

                // Level up button
                if (di.Parent != null)
                {
                    string parentData = $"fmd_{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(di.Parent.FullName))}";
                    buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("⬆️ UP", parentData) });
                }
                else
                {
                    buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("⬆️ ROOT", "file_manager") });
                }

                // Item buttons
                foreach (var item in pagedItems)
                {
                    string prefix = item.IsDir ? "📁" : "📄";
                    string cbPrefix = item.IsDir ? "fmd_" : "fmf_";
                    string shortName = item.Name.Length > 22 ? item.Name.Substring(0, 19) + "..." : item.Name;
                    
                    int pathId = PathCache.Add(item.Path);
                    string cbData = $"{cbPrefix}{pathId}";

                    buttons.Add(new[] { InlineKeyboardButton.WithCallbackData($"{prefix} {shortName}", cbData) });
                }

                // Add a small separator if possible (empty button or just text)
                
                // Pagination buttons
                var navButtons = new List<InlineKeyboardButton>();
                if (page > 0)
                {
                    int parentId = PathCache.Add(path);
                    navButtons.Add(InlineKeyboardButton.WithCallbackData("⏪ Prev", $"fmp_{parentId}_{page - 1}"));
                }
                if (page < totalPages - 1)
                {
                    int parentId = PathCache.Add(path);
                    navButtons.Add(InlineKeyboardButton.WithCallbackData("Next ⏩", $"fmp_{parentId}_{page + 1}"));
                }
                if (navButtons.Count > 0)
                {
                    buttons.Add(navButtons.ToArray());
                }

                string header = $"📂 **DRIVE:** `{Path.GetPathRoot(path)}`\n" +
                                $"📍 **PATH:** `{path}`\n" +
                                $"📄 **Page:** {page + 1}/{Math.Max(1, totalPages)}\n" +
                                $"━━━━━━━━━━━━━━━━━━";

                return (header, new InlineKeyboardMarkup(buttons));

            }
            catch (UnauthorizedAccessException)
            {
                return ($"❌ Access Denied: `{path}`", new InlineKeyboardMarkup(new[] { InlineKeyboardButton.WithCallbackData("⬅️ Home", "file_manager") }));
            }
            catch (Exception ex)
            {
                return ($"❌ Error: {ex.Message}", new InlineKeyboardMarkup(new[] { InlineKeyboardButton.WithCallbackData("⬅️ Home", "file_manager") }));
            }
        }
    }

    // In-memory cache to handle Telegram's 64-byte callback data limit
    public static class PathCache
    {
        private static readonly Dictionary<int, string> _cache = new Dictionary<int, string>();
        private static int _counter = 0;

        public static int Add(string path)
        {
            var existing = _cache.FirstOrDefault(x => x.Value == path);
            if (existing.Key != 0) return existing.Key;

            _counter++;
            _cache[_counter] = path;
            return _counter;
        }

        public static string Get(int id)
        {
            return _cache.ContainsKey(id) ? _cache[id] : null;
        }
    }
}
