using ProfilingBot.Core.Models;

namespace ProfilingBot.Core.Interfaces
{
    public interface IConfigurationService
    {
        // Основные конфигурации
        Task<BotConfig> GetBotConfigAsync();
        Task<List<Question>> GetQuestionsAsync();
        Task<List<PersonalityType>> GetPersonalityTypesAsync();

        // Вспомогательные методы для быстрого доступа
        Task<string> GetWelcomeMessageAsync();
        Task<string> GetIntroMessageAsync();
        Task<string> GetCompletionMessageAsync();

        // Получение конкретных объектов
        Task<Question?> GetQuestionAsync(int questionId);
        Task<PersonalityType?> GetPersonalityTypeAsync(int personalityTypeId);

        // Валидация конфигурации
        Task<bool> ValidateConfigurationAsync();

        // Перезагрузка конфигурации (если файлы изменились)
        Task ReloadConfigurationAsync();
    }
}