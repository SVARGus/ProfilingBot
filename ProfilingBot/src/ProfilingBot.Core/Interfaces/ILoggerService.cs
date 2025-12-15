
namespace ProfilingBot.Core.Interfaces
{
    public interface ILoggerService
    {
        // Основные методы
        void LogInfo(string message);
        void LogWarning(string message);
        void LogError(string message);
        void LogError(Exception exception, string message);

        // Для отладки
        void LogDebug(string message);
    }
}