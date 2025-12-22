using ProfilingBot.Core.Interfaces;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Extensions;

namespace ProfilingBot.Cloud.Handlers
{
    public class CallbackQueryUpdateHandler : UpdateHandler
    {
        public CallbackQueryUpdateHandler(
            TelegramBotClient botClient,
            ITestService testService,
            IConfigurationService configurationService,
            ILoggerService logger)
            : base(botClient, testService, configurationService, logger)
        {
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
            if (parts.Length == 5 &&
                Guid.TryParse(parts[1], out var sessionId) &&
                int.TryParse(parts[2], out var questionId) &&
                int.TryParse(parts[3], out var answerId) &&
                int.TryParse(parts[4], out var displayIndex))
            {
                // Логируем для отладки
                _loggerService.LogInfo($"User selected: session={sessionId}, question={questionId}, answer={answerId}, displayIndex={displayIndex}");

                // Сохраняем ответ (оригинальный questionId → оригинальный answerId)
                var session = await _testService.AnswerQuestionAsync(sessionId, questionId, answerId);

                if (session == null)
                {
                    await _botClient.SendMessage(
                        chatId: chatId,
                        text: "Произошла ошибка при обработке ответа.",
                        cancellationToken: cancellationToken);
                    return;
                }

                if (session.IsCompleted)
                {
                    // Тест завершен - показываем результат
                    await ShowTestResultAsync(session, chatId, cancellationToken);
                }
                else
                {
                    // Показываем следующий вопрос
                    var question = await _testService.GetCurrentQuestionAsync(session.Id);
                    if (question != null)
                    {
                        await SendQuestionAsync(session, question, chatId, cancellationToken);
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