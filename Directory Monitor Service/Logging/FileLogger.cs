using System;
using System.IO;

namespace DirectoryMonitorService.Logging
{
    public class FileLogger
    {
        private readonly string _logFilePath;
        private readonly object _lock = new object();
        private const long MaxLogFileSizeBytes = 5 * 1024 * 1024; // 5 MB

        public FileLogger(string logFilePath)
        {
            _logFilePath = logFilePath;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_logFilePath));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Logger Init Error] Failed to create log directory: {ex.Message}");
            }
        }

        public void Log(string message)
        {
            lock (_lock)
            {
                try
                {
                    RotateLogIfNeeded();

                    File.AppendAllText(_logFilePath,
                        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} | {message}{Environment.NewLine}");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[Logger Error] Failed to write to log file: {ex.Message}");
                }
            }
        }

        private void RotateLogIfNeeded()
        {
            try
            {
                if (File.Exists(_logFilePath))
                {
                    var fileInfo = new FileInfo(_logFilePath);
                    if (fileInfo.Length >= MaxLogFileSizeBytes)
                    {
                        string archiveName = Path.Combine(
                            Path.GetDirectoryName(_logFilePath),
                            $"log_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

                        File.Move(_logFilePath, archiveName);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Logger Error] Failed to rotate log file: {ex.Message}");
            }
        }
    }
}