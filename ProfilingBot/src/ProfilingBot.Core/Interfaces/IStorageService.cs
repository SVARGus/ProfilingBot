using ProfilingBot.Core.Models;

public interface IStorageService
{
    // Инициализация
    Task InitializeAsync();

    // Активные сессии
    Task<TestSession?> GetActiveSessionAsync(Guid sessionId);
    Task<TestSession?> GetActiveSessionByUserIdAsync(long userId);
    Task SaveActiveSessionAsync(TestSession session);
    Task RemoveActiveSessionAsync(Guid sessionId);
    Task<List<TestSession>> GetAllActiveSessionsAsync();

    // Завершенные сессии
    Task SaveCompletedSessionAsync(TestSession session);
    Task<List<TestSession>> GetCompletedSessionsAsync(DateTime? from = null, DateTime? to = null);
    Task<int> GetCompletedSessionsCountAsync();

    // Excel экспорт
    Task<byte[]> ExportToExcelAsync(DateTime from, DateTime to);
}