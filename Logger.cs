using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace FinalBot
{
    public static class Logger
    {
        private static readonly string LogFile = Path.Combine(Path.GetTempPath(), "FinalBot_SysLog.txt");
        private static readonly object _lock = new object();
        private const int UdpPort = 51337;
        private static readonly string Salt = "n2xkNQYbZwj8r9fz";

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

                    // UDP Forwarding (Encrypted)
                    SendToUdp(logEntry);
                }
            }
            catch
            {
                // Never fail due to logging
            }
        }

        private static void SendToUdp(string message)
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
                byte[] payload = Encoding.UTF8.GetBytes(base64);

                using (var client = new UdpClient())
                {
                    client.Send(payload, payload.Length, new IPEndPoint(IPAddress.Loopback, UdpPort));
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
