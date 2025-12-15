using ProfilingBot.Core.Models;

namespace ProfilingBot.Core.Interfaces
{
    public interface ITestService
    {
        // Управление сессиями тестирования
        Task<TestSession> StartTestAsync(long userId, string userName);
        Task<TestSession?> AnswerQuestionAsync(Guid sessionId, int questionId, int answerId);
        Task<TestSession> CompleteTestAsync(Guid sessionId);
        Task<TestSession?> GetActiveSessionAsync(long userId);
        Task<bool> IsTestCompletedAsync(Guid sessionId);

        // Получение вопросов
        Task<Question?> GetCurrentQuestionAsync(Guid sessionId);
        Task<List<Question>> GetAllQuestionsAsync();

        // Подсчет результатов (для завершенной сессии)
        Task<TestResult> CalculateResultAsync(TestSession completedSession);

        // Экспорт
        Task<byte[]> ExportSessionsToExcelAsync(DateTime from, DateTime to);
    }
}