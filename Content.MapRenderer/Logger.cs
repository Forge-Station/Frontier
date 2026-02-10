using System;
using System.IO;

namespace Content.MapRenderer
{
    internal static class Logger
    {
        private static readonly string LogFilePath = "RenderLog.txt";

        public static void Init()
        {
            try
            {
                File.WriteAllText(LogFilePath, string.Empty);
                Log("Started", sendInConsole: false);
            }
            catch {}
        }

        public static void Log(string message, Exception? ex = null, bool sendInConsole = true)
        {
            try
            {
                if (sendInConsole)
                {
                    if (ex != null)
                        Console.Error.WriteLine($"{message}\n{ex}");
                    else
                        Console.WriteLine(message);
                }

                var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}";
                if (ex != null)
                    line += $"Exception details: {ex}{Environment.NewLine}";


                File.AppendAllText(LogFilePath, line);
            }
            catch {}
        }
    }
}
