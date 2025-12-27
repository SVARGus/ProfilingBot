using ProfilingBot.Core.Interfaces;
using ProfilingBot.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProfilingBot.Core.Services
{
    public class TestService : ITestService
    {
        private readonly IConfigurationService _configService;
        private readonly IStorageService _storageService;
        private readonly ILoggerService _logger;

        public TestService(
            IConfigurationService configService,
            IStorageService storageService,
            ILoggerService logger)
        {
            _configService = configService;
            _storageService = storageService;
            _logger = logger;

            _logger.LogInfo("TestService initialized");
        }

        public async Task<TestSession> StartTestAsync(long userId, string userName)
        {
            _logger.LogInfo($"Starting test for user {userId} ({userName})");

            try
            {
                // Проверяем, есть ли активная сессия
                var existingSession = await GetActiveSessionAsync(userId);
                if (existingSession != null)
                {
                    _logger.LogWarning($"User {userId} already has active session {existingSession.Id}");
                    return existingSession;
                }

                var allQuestions = await _configService.GetQuestionsAsync();

                var session = new TestSession
                {
                    UserId = userId,
                    UserName = userName,
                    StartedAt = DateTime.UtcNow,
                    CurrentQuestionIndex = 1
                };

                // ========== ДОБАВЛЕНО: Рандомизация ==========
                var rnd = new Random(Guid.NewGuid().GetHashCode());

                // 1. Перемешиваем вопросы
                var shuffledQuestions = allQuestions.OrderBy(q => rnd.Next()).ToList();
                session.QuestionOrder = shuffledQuestions.Select(q => q.Id).ToList();

                // 2. Для каждого вопроса перемешиваем варианты ответов
                foreach (var question in allQuestions)
                {
                    var shuffledAnswers = question.Answers.OrderBy(a => rnd.Next()).ToList();
                    session.AnswerOrder[question.Id] = shuffledAnswers.Select(a => a.Id).ToList();
                }
                // =============================================

                await _storageService.SaveActiveSessionAsync(session);

                _logger.LogInfo($"Session {session.Id} created for user {userId}");
                _logger.LogDebug($"Question order: [{string.Join(", ", session.QuestionOrder)}]");

                return session;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to start test for user {userId}");
                throw;
            }
        }

        public async Task<TestSession?> AnswerQuestionAsync(Guid sessionId, int questionId, int answerId)
        {
            var session = await _storageService.GetActiveSessionAsync(sessionId);
            if (session == null)
            {
                _logger.LogWarning($"Session {sessionId} not found");
                return null;
            }

            if (session.IsCompleted)
            {
                _logger.LogWarning($"Session {sessionId} already completed");
                return null;
            }

            var questions = await _configService.GetQuestionsAsync();
            var question = questions.FirstOrDefault(q => q.Id == questionId);

            if (question == null)
            {
                _logger.LogError($"Question {questionId} not found");
                return null;
            }

            if (!question.Answers.Any(a => a.Id == answerId))
            {
                _logger.LogError($"Answer {answerId} not found for question {questionId}");
                return null;
            }

            // Сохраняем ответ
            session.Answers[questionId] = answerId;

            // Обновляем текущий вопрос
            var nextQuestionIndex = session.CurrentQuestionIndex + 1;
            var totalQuestions = (await _configService.GetBotConfigAsync()).TotalQuestions;

            if (nextQuestionIndex <= totalQuestions)
            {
                session.CurrentQuestionIndex = nextQuestionIndex;
                // Сохраняем обновлённую сессию
                await _storageService.SaveActiveSessionAsync(session);

                _logger.LogDebug($"User {session.UserId} answered question {questionId} with answer {answerId}");
                _logger.LogDebug($"Progress: {session.Answers.Count}/{totalQuestions} questions answered");

                return session;
            }
            else
            {
                // Все вопросы отвечены - сохраняем сессию С 8-м ответом
                await _storageService.SaveActiveSessionAsync(session);

                _logger.LogInfo($"All questions answered for session {sessionId}");
                _logger.LogInfo($"Total answers before completion: {session.Answers.Count}");

                // Теперь завершаем тест
                var completedSession = await CompleteTestAsync(sessionId);

                _logger.LogInfo($"Test completed. Total answers: {completedSession.Answers.Count}");

                return completedSession;
            }
        }

        public async Task<TestSession> CompleteTestAsync(Guid sessionId)
        {
            var session = await _storageService.GetActiveSessionAsync(sessionId);
            if (session == null)
            {
                throw new InvalidOperationException($"Session {sessionId} not found");
            }

            if (session.IsCompleted)
            {
                throw new InvalidOperationException($"Session {sessionId} already completed");
            }

            // Помечаем как завершенную
            session.CompletedAt = DateTime.UtcNow;

            // Рассчитываем результат перед сохранением
            var result = await CalculateResultAsync(session);

            // Переносим в завершенные
            await _storageService.SaveCompletedSessionAsync(session);
            await _storageService.RemoveActiveSessionAsync(sessionId);

            _logger.LogInfo($"Test completed for user {session.UserId}, session {session.Id}, type: {session.ResultNamePersonalityType}");

            return session;
        }

        public async Task<TestSession?> GetActiveSessionAsync(long userId)
        {
            return await _storageService.GetActiveSessionByUserIdAsync(userId);
        }

        public async Task<bool> IsTestCompletedAsync(Guid sessionId)
        {
            var session = await _storageService.GetActiveSessionAsync(sessionId);
            return session?.IsCompleted ?? false;
        }

        public async Task<Question?> GetCurrentQuestionAsync(Guid sessionId)
        {
            var session = await _storageService.GetActiveSessionAsync(sessionId);
            if (session == null || session.IsCompleted)
            {
                return null;
            }

            var questions = await _configService.GetQuestionsAsync();

            if (session.CurrentQuestionIndex >= 1 && session.CurrentQuestionIndex <= session.QuestionOrder.Count)
            {
                var originalQuestionId = session.QuestionOrder[session.CurrentQuestionIndex - 1];
                return questions.FirstOrDefault(q => q.Id == originalQuestionId);
            }

            return null;
        }

        public async Task<List<Question>> GetAllQuestionsAsync()
        {
            return await _configService.GetQuestionsAsync();
        }

        public async Task<byte[]> ExportSessionsToExcelAsync(DateTime from, DateTime to)
        {
            return await _storageService.ExportToExcelAsync(from, to);
        }

        public async Task<TestResult> CalculateResultAsync(TestSession completedSession)
        {
            if (!completedSession.IsCompleted || !completedSession.CompletedAt.HasValue)
            {
                throw new InvalidOperationException("Session is not completed");
            }

            var questions = await _configService.GetQuestionsAsync();
            var personalityTypes = await _configService.GetPersonalityTypesAsync();

            // Подсчет баллов
            var scores = CalculateScores(completedSession, questions, personalityTypes);

            // Определение типа личности
            var personalityTypeId = DeterminePersonalityType(scores, personalityTypes);

            // Обновляем сессию с результатом
            completedSession.ResultIdPersonalityType = personalityTypeId;

            var personalityType = personalityTypes.First(t => t.Id == personalityTypeId);
            completedSession.ResultNamePersonalityType = personalityType.Name;

            // Сохраняем обновленную сессию
            await _storageService.SaveCompletedSessionAsync(completedSession);

            // Создаем DTO результат
            return new TestResult(
                sessionId: completedSession.Id,
                userId: completedSession.UserId,
                userName: completedSession.UserName,
                startedAt: completedSession.StartedAt,
                completedAt: completedSession.CompletedAt.Value,
                personalityTypeId: personalityTypeId,
                scores: scores
            );
        }

        private int[] CalculateScores(TestSession session, List<Question> questions, List<PersonalityType> personalityTypes)
        {
            var maxTypeId = personalityTypes.Max(t => t.Id);
            var scores = new int[maxTypeId + 1];

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
    }
}