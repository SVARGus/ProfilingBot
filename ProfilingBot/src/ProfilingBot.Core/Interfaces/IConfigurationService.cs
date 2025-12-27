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

        /// <summary>
        /// Получить конфигурацию генерации карточек
        /// </summary>
        Task<CardGenerationConfig> GetCardGenerationConfigAsync();

        /// <summary>
        /// Получить базовый путь к корневой директории приложения
        /// (для определения абсолютных путей к assets)
        /// </summary>
        string GetBasePath();

        /// <summary>
        /// Получить абсолютный путь к директории с карточками
        /// </summary>
        Task<string> GetCardsDirectoryPathAsync();

        /// <summary>
        /// Получить абсолютный путь к файлу/директории относительно базовой директории
        /// </summary>
        string GetAbsolutePath(string relativePath);
    }
}