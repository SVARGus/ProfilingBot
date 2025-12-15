using ProfilingBot.Core.Models;

namespace ProfilingBot.Core.Interfaces
{
    public interface IResultGeneratorService
    {
        // Генерация результата из завершенной сессии (без сохранения)
        Task<TestResult> GenerateResultAsync(TestSession completedSession);

        // Генерация текстового сообщения для пользователя
        Task<string> GenerateResultMessageAsync(TestResult result, PersonalityType personalityType);

        // Генерация shareable карточки
        Task<TestResult> GenerateShareableCardAsync(TestResult result, PersonalityType personalityType);

        // Получение личности по ID (из конфигурации)
        Task<PersonalityType?> GetPersonalityTypeAsync(int personalityTypeId);
    }
}