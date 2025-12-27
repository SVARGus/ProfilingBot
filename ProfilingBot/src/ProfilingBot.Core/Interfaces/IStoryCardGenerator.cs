using ProfilingBot.Core.Models;

namespace ProfilingBot.Core.Interfaces
{
    /// <summary>
    /// Генератор персонализированных карточек с результатами теста
    /// </summary>
    public interface IStoryCardGenerator
    {
        /// <summary>
        /// Генерирует карточку для истории/шеринга
        /// </summary>
        /// <param name="result">Результат теста</param>
        /// <param name="personalityType">Тип личности</param>
        /// <param name="cancellationToken">Токен отмены</param>
        /// <returns>Карточка в формате JPEG</returns>
        Task<byte[]> GenerateCardAsync(
            TestResult result,
            PersonalityType personalityType,
            CancellationToken cancellationToken = default);
    }
}