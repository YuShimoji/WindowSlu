using System;
using System.IO;
using System.Text;
using System.Diagnostics;

namespace WindowSlu.Services
{
    public static class LoggingService
    {
        private static readonly string LogFilePath = Path.Combine(AppContext.BaseDirectory, "debug_output.log");
        private static readonly string errorLogFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log");
        private static readonly object lockObject = new object();
        private const string EventLogSource = "WindowSluApp";

        public static void LogInfo(string message)
        {
            Log("INFO", message);
        }

        public static void LogError(string message, Exception? ex = null)
        {
            Log("ERROR", message);
            if (ex != null)
            {
                Log("ERROR", $"Exception details: {ex}");
            }
        }

        private static void Log(string level, string message)
        {
            try
            {
                string logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - [{level}] {message}{Environment.NewLine}";
                File.AppendAllText(LogFilePath, logMessage);
            }
            catch
            {
                // ログ書き込みに失敗した場合は無視
            }
        }

        private static void LogErrorToEventLog(string message, Exception? ex = null)
            {
                try
                {
                    // EventSource が存在しない場合は作成 (管理者権限が必要な場合がある)
                    if (!EventLog.SourceExists(EventLogSource))
                    {
                        EventLog.CreateEventSource(EventLogSource, "Application");
                    }

                    StringBuilder eventLogMessage = new StringBuilder();
                    eventLogMessage.AppendLine($"!!! LoggingService Critical Error: Failed to write to log file. Path: {errorLogFilePath} !!!");
                eventLogMessage.AppendLine($"LoggingService Error Details: {ex?.ToString() ?? "N/A"}");
                    eventLogMessage.AppendLine($"Original Error attempting to log: Message='{message}', Exception='{ex?.ToString() ?? "N/A"}'");

                    EventLog.WriteEntry(EventLogSource, eventLogMessage.ToString(), EventLogEntryType.Error);
                }
                catch (Exception eventLogEx)
                {
                    // If writing to EventLog also fails, there's not much more we can do.
                    // Avoid further Console.WriteLine to prevent potential loops or further issues in production.
                    Debug.WriteLine($"!!! LoggingService Critical Error: Failed to write to EventLog. EventLogException: {eventLogEx.ToString()} !!!");
                Debug.WriteLine($"Original LoggingService Error: {ex?.ToString() ?? "N/A"}");
                    Debug.WriteLine($"Original Error attempting to log: Message='{message}', Exception='{ex?.ToString() ?? "N/A"}'");
            }
        }
    }
} 