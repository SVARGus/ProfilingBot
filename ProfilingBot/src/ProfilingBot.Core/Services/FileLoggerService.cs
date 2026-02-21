using ProfilingBot.Core.Interfaces;

namespace ProfilingBot.Core.Services
{
    public class FileLoggerService : ILoggerService
    {
        private readonly string _logsDirectory;
        private readonly string _logFilePath;
        private readonly object _lock = new();

        public FileLoggerService(string logsDirectory)
        {
            _logsDirectory = logsDirectory;
            // Создаем директорию если её нет
            if (!Directory.Exists(logsDirectory))
            {
                Directory.CreateDirectory(logsDirectory);
            }

            // Файл логов на текущий день
            var date = DateTime.Now.ToString("yyyyMMdd");
            _logFilePath = Path.Combine(logsDirectory, $"bot_{date}.log");
        }

        public void LogInfo(string message)
        {
            WriteLog("INFO", message);
        }

        public void LogWarning(string message)
        {
            WriteLog("WARN", message);
        }

        public void LogError(string message)
        {
            WriteLog("ERROR", message);
        }

        public void LogError(Exception exception, string message)
        {
            WriteLog("ERROR", $"{message} | {exception.Message}");
        }

        public void LogDebug(string message)
        {
            // В продакшне можно отключать через настройки
#if DEBUG
            WriteLog("DEBUG", message);
#endif
        }

        public string GetLogsDirectory() => _logsDirectory;

        public List<string> GetLogFiles(int lastDays = 7)
        {
            var files = new List<string>();
            if (!Directory.Exists(_logsDirectory))
                return files;

            for (int i = 0; i < lastDays; i++)
            {
                var date = DateTime.Now.AddDays(-i).ToString("yyyyMMdd");
                var filePath = Path.Combine(_logsDirectory, $"bot_{date}.log");
                if (File.Exists(filePath))
                {
                    files.Add(filePath);
                }
            }
            return files;
        }

        private void WriteLog(string level, string message)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var logEntry = $"[{timestamp}] [{level}] {message}";

            lock (_lock)
            {
                try
                {
                    File.AppendAllText(_logFilePath, logEntry + Environment.NewLine);
                    Console.WriteLine(logEntry); // Дублируем в консоль для отладки
                }
                catch (Exception ex)
                {
                    // Если не удалось записать в файл - только в консоль
                    Console.WriteLine($"Failed to write log: {ex.Message}");
                    Console.WriteLine(logEntry);
                }
            }
        }
    }
}