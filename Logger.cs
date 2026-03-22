using System;
using System.IO;

namespace FinalBot
{
    public static class Logger
    {
        private static readonly string LogFile = Path.Combine(Path.GetTempPath(), "FinalBot_SysLog.txt");
        private static readonly object _lock = new object();

        public static void Log(string message, string level = "INFO")
        {
            try
            {
                lock (_lock)
                {
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    string logEntry = $"[{timestamp}] [{level}] {message}";
                    
                    // Console output for debugging
                    ConfigureConsoleColor(level);
                    Console.WriteLine(logEntry);
                    Console.ResetColor();

                    // File logging
                    File.AppendAllText(LogFile, logEntry + Environment.NewLine);
                }
            }
            catch
            {
                // Never fail due to logging
            }
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
