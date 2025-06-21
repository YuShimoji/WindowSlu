using System;
using System.IO;

namespace WindowSlu.Services
{
    public static class LoggingService
    {
        private static readonly string LogFilePath;
        private static readonly object _lock = new object();

        static LoggingService()
        {
            // ログファイルのパスを %AppData%\WindowSlu\Logs に設定
            string logDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WindowSlu", "Logs");
            Directory.CreateDirectory(logDirectory); // フォルダがなければ作成
            LogFilePath = Path.Combine(logDirectory, $"log_{DateTime.Now:yyyyMMdd}.txt");
        }

        private static void WriteLine(string level, string message)
        {
            // スレッドセーフにファイルに書き込む
            lock (_lock)
            {
                try
                {
                    using (var writer = new StreamWriter(LogFilePath, true))
                    {
                        writer.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}");
                    }
                }
                catch (Exception)
                {
                    // ログの書き込み自体でエラーが発生した場合は、なにもしない
                }
            }
        }

        public static void LogInfo(string message)
        {
            WriteLine("INFO", message);
        }

        public static void LogError(string message)
        {
            WriteLine("ERROR", message);
        }
    }
} 