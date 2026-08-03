using System;
using System.IO;
using EGM.Core.Interfaces;
using EGM.Core.Enums;
using EGM.Core.Infrastructure;

namespace EGM.Core.Services
{
    public class LoggerService : ILogger
    {
        private readonly string _logFilePath;
        private readonly object _fileLock = new();
        private readonly string _errorLogFilePath;
        private const long _maxLogSizeBytes = 5 * 1024 * 1024;
        public LoggerService()
        {
            string dataDir = FileFunctions.LogDirectory;
            _logFilePath = Path.Combine(dataDir, "system.log");
            _errorLogFilePath = Path.Combine(dataDir, "system.err");
        }

        public void Log(LogTypeEnum logType, string message)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string entry = $"[{timestamp}] [{logType.ToString().ToUpper()}] {message}";

            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = logType switch
            {
                LogTypeEnum.Error => ConsoleColor.Red,
                LogTypeEnum.Warning => ConsoleColor.Yellow,
                _ => ConsoleColor.White
            };
            Console.WriteLine(entry);
            Console.ForegroundColor = originalColor;

            AppendToFile(entry);
        }

        public void Audit(string actor, string action, string oldValue, string newValue)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string entry = $"[{timestamp}] [AUDIT] User: {actor} | Action: {action} | Old: '{oldValue}' -> New: '{newValue}'";

            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(entry);
            Console.ForegroundColor = originalColor;

            AppendToFile(entry);
        }

        private void AppendToFile(string message)
        {
            lock (_fileLock)
            {
                try
                {
                    CheckAndRotateLog();
                    File.AppendAllText(_logFilePath, message + Environment.NewLine);
                }
                catch(Exception ex)
                {
                    LogWithFallback(message, ex.Message);
                }
            }
        }
        private void CheckAndRotateLog()
        {
            var fileInfo = new FileInfo(_logFilePath);

            // If file doesn't exist or is small enough, do nothing
            if (!fileInfo.Exists || fileInfo.Length < _maxLogSizeBytes) return;

            try
            {
                // Create a backup filename: system_20231027_153000.log
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string backupPath = _logFilePath.Replace(".log", $"_{timestamp}.log");

                // Rename current file to backup
                fileInfo.MoveTo(backupPath);
            }
            catch (Exception ex)
            {
               LogWithFallback($"Failed to rotate log file", ex.Message);
            }
        }

        private void LogWithFallback(string originalMessage,string message)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                string fallbackEntry =
                    $"[{timestamp}] [LOGGER-FAILURE] Failed to write log entry.\n" +
                    $"Original Message: {originalMessage}\n" +
                    $"Exception: {message}\n";
                Console.WriteLine($"[CRITICAL] {fallbackEntry}");
                File.AppendAllText(_errorLogFilePath, fallbackEntry + Environment.NewLine);
            }
            catch
            {
                Console.WriteLine($"[CRITICAL] Failed to log with fall back : {message}");
            }
        }
    }
}