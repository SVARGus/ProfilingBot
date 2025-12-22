using ProfilingBot.Core.Interfaces;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Extensions;

namespace ProfilingBot.Cloud.Handlers
{
    public class CallbackQueryUpdateHandler : UpdateHandler
    {
        private readonly IStorageService _storageService;
        public CallbackQueryUpdateHandler(
            TelegramBotClient botClient,
            ITestService testService,
            IConfigurationService configurationService,
            ILoggerService logger,
            IStorageService storageService)
            : base(botClient, testService, configurationService, logger)
        {
            _storageService = storageService;
        }

        public override bool CanHandle(Update update)
        {
            return update.CallbackQuery != null;
        }

        public override async Task HandleAsync(Update update, CancellationToken cancellationToken)
        {
            var callbackQuery = update.CallbackQuery!;
            var callbackData = callbackQuery.Data;
            var userId = callbackQuery.From.Id;
            var chatId = callbackQuery.Message!.Chat.Id;

            _loggerService.LogInfo($"Processing callback from user {userId}: {callbackData}");

            // Обработка ответов на вопросы
            if (callbackData?.StartsWith("answer_") == true)
            {
                await HandleAnswerCallbackAsync(callbackData, userId, chatId, cancellationToken);
            }
            // Обработка "Продолжить тест"
            else if (callbackData?.StartsWith("continue_") == true)
            {
                await HandleContinueCallbackAsync(callbackData, userId, chatId, cancellationToken);
            }
            // Обработка "Начать заново"
            else if (callbackData?.StartsWith("restart_") == true)
            {
                await HandleRestartCallbackAsync(callbackData, userId, chatId, cancellationToken);
            }
            // Обработка действий с результатом
            else if (callbackData?.StartsWith("getcard_") == true ||
                     callbackData?.StartsWith("share_") == true)
            {
                await HandleResultActionCallbackAsync(callbackData, userId, chatId, cancellationToken);
            }

            // Отвечаем на callback (убираем "часики" у кнопки)
            await _botClient.AnswerCallbackQuery(
                callbackQueryId: callbackQuery.Id,
                cancellationToken: cancellationToken);
        }

        private async Task HandleAnswerCallbackAsync(
            string callbackData,
            long userId,
            long chatId,
            CancellationToken cancellationToken)
        {
            var parts = callbackData.Split('_');

            // Новый формат: answer_{sessionId}_{displayIndex}
            if (parts.Length == 3 &&
                Guid.TryParse(parts[1], out var sessionId) &&
                int.TryParse(parts[2], out var displayIndex))
            {
                // Получаем сессию
                var session = await _storageService.GetActiveSessionAsync(sessionId);

                if (session == null)
                {
                    _loggerService.LogError($"Session {sessionId} not found");
                    return;
                }

                // Получаем текущий вопрос
                var currentQuestion = await _testService.GetCurrentQuestionAsync(sessionId);
                if (currentQuestion == null)
                {
                    _loggerService.LogError($"Current question not found for session {sessionId}");
                    return;
                }

                // Проверяем валидность displayIndex (1-5)
                if (displayIndex < 1 || displayIndex > 5)
                {
                    _loggerService.LogError($"Invalid displayIndex: {displayIndex}");
                    return;
                }

                // Получаем оригинальный AnswerId из порядка в сессии
                if (!session.AnswerOrder.TryGetValue(currentQuestion.Id, out var answerOrder))
                {
                    _loggerService.LogError($"Answer order not found for question {currentQuestion.Id}");
                    return;
                }

                if (displayIndex > answerOrder.Count)
                {
                    _loggerService.LogError($"DisplayIndex {displayIndex} out of range for question {currentQuestion.Id}");
                    return;
                }

                var originalAnswerId = answerOrder[displayIndex - 1]; // displayIndex is 1-based

                _loggerService.LogInfo($"User selected: session={sessionId}, question={currentQuestion.Id}, " +
                                      $"displayIndex={displayIndex}, answerId={originalAnswerId}");

                // Сохраняем ответ через TestService
                var updatedSession = await _testService.AnswerQuestionAsync(sessionId, currentQuestion.Id, originalAnswerId);

                if (updatedSession == null)
                {
                    await _botClient.SendMessage(
                        chatId: chatId,
                        text: "Произошла ошибка при обработке ответа.",
                        cancellationToken: cancellationToken);
                    return;
                }

                if (updatedSession.IsCompleted)
                {
                    // Тест завершен - показываем результат
                    await ShowTestResultAsync(updatedSession, chatId, cancellationToken);
                }
                else
                {
                    // Показываем следующий вопрос
                    var nextQuestion = await _testService.GetCurrentQuestionAsync(updatedSession.Id);
                    if (nextQuestion != null)
                    {
                        await SendQuestionAsync(updatedSession, nextQuestion, chatId, cancellationToken);
                    }
                }
            }
            else
            {
                _loggerService.LogError($"Invalid callback data format: {callbackData}");
            }
        }

        private async Task HandleContinueCallbackAsync(string callbackData, long userId, long chatId, CancellationToken cancellationToken)
        {
            var sessionIdStr = callbackData.Substring("continue_".Length);
            if (Guid.TryParse(sessionIdStr, out var sessionId))
            {
                var session = await _testService.GetActiveSessionAsync(userId);
                if (session == null || session.Id != sessionId)
                {
                    await _botClient.SendMessage(
                        chatId: chatId,
                        text: "❌ Сессия не найдена.",
                        cancellationToken: cancellationToken);
                    return;
                }

                await _botClient.SendMessage(
                    chatId: chatId,
                    text: "✅ Продолжаем тест!",
                    cancellationToken: cancellationToken);

                await Task.Delay(500, cancellationToken);

                var question = await _testService.GetCurrentQuestionAsync(sessionId);
                if (question != null)
                {
                    await SendQuestionAsync(session, question, chatId, cancellationToken);
                }
            }
        }

        private async Task HandleRestartCallbackAsync(string callbackData, long userId, long chatId, CancellationToken cancellationToken)
        {
            var sessionIdStr = callbackData.Substring("restart_".Length);
            if (Guid.TryParse(sessionIdStr, out var oldSessionId))
            {
                // Можно добавить логику архивации старой сессии
                var userName = $"User_{userId}";
                var session = await _testService.StartTestAsync(userId, userName);

                await _botClient.SendMessage(
                    chatId: chatId,
                    text: "🔄 Начинаем тест заново!",
                    cancellationToken: cancellationToken);

                await Task.Delay(500, cancellationToken);

                var question = await _testService.GetCurrentQuestionAsync(session.Id);
                if (question != null)
                {
                    await SendQuestionAsync(session, question, chatId, cancellationToken);
                }
            }
        }

        private async Task HandleResultActionCallbackAsync(string callbackData, long userId, long chatId, CancellationToken cancellationToken)
        {
            // TODO: Реализовать генерацию карточки и шеринг
            await _botClient.SendMessage(
                chatId: chatId,
                text: "Эта функция скоро будет доступна!",
                cancellationToken: cancellationToken);
        }
    }
}