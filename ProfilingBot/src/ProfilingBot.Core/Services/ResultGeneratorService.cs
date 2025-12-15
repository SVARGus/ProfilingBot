using ProfilingBot.Core.Interfaces;
using ProfilingBot.Core.Models;
using System.Text;

namespace ProfilingBot.Core.Services
{
    public class ResultGeneratorService : IResultGeneratorService
    {
        private readonly IConfigurationService _configService;
        private readonly ILoggerService _logger;

        public ResultGeneratorService(
            IConfigurationService configService,
            ILoggerService logger)
        {
            _configService = configService;
            _logger = logger;
        }

        public async Task<TestResult> GenerateResultAsync(TestSession completedSession)
        {
            if (!completedSession.IsCompleted || !completedSession.CompletedAt.HasValue)
            {
                throw new InvalidOperationException("Session is not completed");
            }

            _logger.LogInfo($"Generating result for session {completedSession.Id}");

            var questions = await _configService.GetQuestionsAsync();
            var personalityTypes = await _configService.GetPersonalityTypesAsync();

            // Подсчет баллов, возможно излишне
            var scores = CalculateScores(completedSession, questions, personalityTypes);

            // Определение типа личности (приоритет по ID)
            var personalityTypeId = DeterminePersonalityType(scores, personalityTypes);

            // Создаем DTO результат
            var result = new TestResult(
                sessionId: completedSession.Id,
                userId: completedSession.UserId,
                userName: completedSession.UserName,
                startedAt: completedSession.StartedAt,
                completedAt: completedSession.CompletedAt.Value,
                personalityTypeId: personalityTypeId,
                scores: scores
            );

            _logger.LogInfo($"Result generated for session {result.SessionId}. Type ID: {result.PersonalityTypeId}");

            return result;
        }

        public async Task<string> GenerateResultMessageAsync(TestResult result, PersonalityType personalityType)
        {
            var completionMessage = await _configService.GetCompletionMessageAsync();

            var message = $@"{completionMessage}

                🎯 *{personalityType.FullName}*

                {personalityType.Description}

                ✨ {personalityType.Sphere}

                💪 {personalityType.Strengths}

                🖼 {personalityType.Recommendations}

                📊 *Ваши баллы по типам:*
                Социальный: {result.Scores[1]}
                Творческий: {result.Scores[2]}
                Технический: {result.Scores[3]}
                Аналитический: {result.Scores[4]}
                Натуралистический: {result.Scores[5]}

                Хотите получить красивую карточку с результатом?";

            return message;
        }

        public async Task<TestResult> GenerateShareableCardAsync(TestResult result, PersonalityType personalityType)
        {
            if (result.HasShareableCard)
            {
                _logger.LogDebug($"Shareable card already exists for session {result.SessionId}");
                return result;
            }

            _logger.LogInfo($"Generating shareable card for session {result.SessionId}");

            // Генерация карточки (пока текстовая версия)
            var card = await GenerateCardTextAsync(result, personalityType);
            result.SetShareableCard(card);

            return result;
        }

        public async Task<PersonalityType?> GetPersonalityTypeAsync(int personalityTypeId)
        {
            var personalityTypes = await _configService.GetPersonalityTypesAsync();
            return personalityTypes.FirstOrDefault(t => t.Id == personalityTypeId);
        }

        // === Вспомогательные методы ===

        private int[] CalculateScores(TestSession session, List<Question> questions, List<PersonalityType> personalityTypes)
        {
            var maxTypeId = personalityTypes.Max(t => t.Id);
            var scores = new int[maxTypeId + 1]; // scores[0] не используется

            foreach (var answerEntry in session.Answers)
            {
                var questionId = answerEntry.Key;
                var answerId = answerEntry.Value;

                var question = questions.First(q => q.Id == questionId);
                var answer = question.Answers.First(a => a.Id == answerId);

                scores[answer.IdPersonalityType]++;
            }

            return scores;
        }

        private int DeterminePersonalityType(int[] scores, List<PersonalityType> personalityTypes)
        {
            int maxScore = -1;
            int personalityTypeId = -1;

            foreach (var type in personalityTypes.OrderBy(t => t.Id))
            {
                var score = scores[type.Id];

                if (score > maxScore)
                {
                    maxScore = score;
                    personalityTypeId = type.Id;
                }
            }

            if (personalityTypeId == -1)
            {
                personalityTypeId = personalityTypes.OrderBy(t => t.Id).First().Id;
            }

            return personalityTypeId;
        }

        private async Task<byte[]> GenerateCardTextAsync(TestResult result, PersonalityType personalityType)
        {
            // TODO: В будущем заменить на генерацию реального изображения

            var cardText = $@"
                ━━━━━━━━━━━━━━━━━━━━
                       🎯 РЕЗУЛЬТАТ ТЕСТА
                ━━━━━━━━━━━━━━━━━━━━

                👤 {result.UserName}
                📅 {result.CompletedAt:dd.MM.yyyy}

                🏆 {personalityType.FullName}

                {personalityType.Description}

                ✨ {personalityType.Sphere}
                💪 {personalityType.Strengths}
                🖼 {personalityType.Recommendations}

                📊 БАЛЛЫ:
                • Социальный: {result.Scores[1]}
                • Творческий: {result.Scores[2]}
                • Технический: {result.Scores[3]}
                • Аналитический: {result.Scores[4]}
                • Натуралистический: {result.Scores[5]}

                ━━━━━━━━━━━━━━━━━━━━
                Пройти тест: @YourBotName
                ━━━━━━━━━━━━━━━━━━━━
                ";

            return Encoding.UTF8.GetBytes(cardText);
        }
    }
}