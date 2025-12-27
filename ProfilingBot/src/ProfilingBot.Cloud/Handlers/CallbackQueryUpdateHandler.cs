using Microsoft.Extensions.DependencyInjection;
using ProfilingBot.Core.Interfaces;
using ProfilingBot.Core.Models;
using Telegram.Bot;
using Telegram.Bot.Extensions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace ProfilingBot.Cloud.Handlers
{
    public class CallbackQueryUpdateHandler : UpdateHandler
    {
        private readonly IStorageService _storageService;
        private readonly IServiceProvider _serviceProvider;

        public CallbackQueryUpdateHandler(
            TelegramBotClient botClient,
            ITestService testService,
            IConfigurationService configurationService,
            ILoggerService logger,
            IStorageService storageService,
            IServiceProvider serviceProvider)
            : base(botClient, testService, configurationService, logger)
        {
            _storageService = storageService;
            _serviceProvider = serviceProvider;
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

        private async Task HandleContinueCallbackAsync(
            string callbackData, 
            long userId, 
            long chatId, 
            CancellationToken cancellationToken)
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

        private async Task HandleRestartCallbackAsync(
            string callbackData, 
            long userId, 
            long chatId, 
            CancellationToken cancellationToken)
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

        private async Task HandleResultActionCallbackAsync(
            string callbackData, 
            long userId, 
            long chatId, 
            CancellationToken cancellationToken)
        {
            var parts = callbackData.Split('_');

            if (parts.Length != 2 || !Guid.TryParse(parts[1], out var sessionId))
            {
                _loggerService.LogError($"Invalid callback data formatr: {callbackData}");
                await _botClient.SendMessage(
                    chatId: chatId,
                    text: "❌ Ошибка в данных запроса.",
                    cancellationToken: cancellationToken);
                return;
            }

            var action = parts[0];

            try
            {
                var completedSessions = await _storageService.GetCompletedSessionsAsync();
                var session = completedSessions.FirstOrDefault(s => s.Id == sessionId && s.UserId == userId);

                if (session == null)
                {
                    _loggerService.LogWarning($"Session {sessionId} not found or doesn't belong to user {userId}");
                    await _botClient.SendMessage(
                        chatId: chatId,
                        text: "❌ Сессия не найдена или у вас нет доступа.",
                        cancellationToken: cancellationToken);
                    return;
                }

                if (!session.IsCompleted)
                {
                    _loggerService.LogWarning($"Session {sessionId} is not completed");
                    await _botClient.SendMessage(
                        chatId: chatId,
                        text: "❌ Тест еще не завершен.",
                        cancellationToken: cancellationToken);
                    return;
                }

                if (session.ResultIdPersonalityType == 0 || string.IsNullOrEmpty(session.ResultNamePersonalityType))
                {
                    _loggerService.LogWarning($"Session {sessionId} has no result calculated");
                    await _botClient.SendMessage(
                        chatId: chatId,
                        text: "❌ Результат теста не рассчитан. Пожалуйста, пройдите тест заново.",
                        cancellationToken: cancellationToken);
                    return;
                }

                var personalityType = await _configurationService.GetPersonalityTypeAsync(session.ResultIdPersonalityType);
                if (personalityType == null)
                {
                    _loggerService.LogError($"Personality type {session.ResultIdPersonalityType} not found for session {sessionId}");
                    await _botClient.SendMessage(
                        chatId: chatId,
                        text: "❌ Тип личности не найден в конфигурации.",
                        cancellationToken: cancellationToken);
                    return;
                }

                if (action == "getcard")
                {
                    await HandleGetCardActionAsync(session, personalityType, chatId, cancellationToken);
                }
                else if (action == "share")
                {
                    await HandleShareActionAsync(session, personalityType, chatId, cancellationToken);
                }
                else
                {
                    _loggerService.LogWarning($"Unknown action: {action}");
                }
            }
            catch (Exception ex)
            {
                _loggerService.LogError(ex, $"Error handling result action callback: {callbackData}");
                await _botClient.SendMessage(
                    chatId: chatId,
                    text: "❌ Произошла внутренняя ошибка. Пожалуйста, попробуйте позже.",
                    cancellationToken: cancellationToken);
            }
        }

        private async Task HandleGetCardActionAsync(
            TestSession session,
            PersonalityType personalityType,
            long chatId,
            CancellationToken cancellationToken)
        {
            try
            {
                // Уведомляем пользователя о начале генерации
                await _botClient.SendMessage(
                    chatId: chatId,
                    text: "🖼️ Генерирую вашу персонализированную карточку...",
                    cancellationToken: cancellationToken);

                // Используем TestService для расчета результата
                var result = await _testService.CalculateResultAsync(session);

                // Получаем генератор карточек через ServiceProvider
                using var scope = _serviceProvider.CreateScope();
                var cardGenerator = scope.ServiceProvider.GetRequiredService<IStoryCardGenerator>();

                // Генерируем карточку
                var cardImage = await cardGenerator.GenerateCardAsync(
                    result,
                    personalityType,
                    cancellationToken);

                // Отправляем картинку
                using var stream = new MemoryStream(cardImage);
                await _botClient.SendPhoto(
                    chatId: chatId,
                    photo: InputFile.FromStream(stream, "result.jpg"),
                    caption: $"🎯 {personalityType.FullName}\n👤 {session.UserName}\n\n📅 {session.CompletedAt:dd.MM.yyyy}",
                    cancellationToken: cancellationToken);

                _loggerService.LogInfo($"Card generated and sent for session {session.Id}");
            }
            catch (Exception ex)
            {
                _loggerService.LogError(ex, $"Failed to generate card for session {session.Id}");

                await _botClient.SendMessage(
                    chatId: chatId,
                    text: "❌ Не удалось сгенерировать карточку. Пожалуйста, попробуйте позже.",
                    cancellationToken: cancellationToken);
            }
        }

        private async Task HandleShareActionAsync(
            TestSession session,
            PersonalityType personalityType,
            long chatId,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _testService.CalculateResultAsync(session);
                var completionMessage = await _configurationService.GetCompletionMessageAsync();

                var shareText = $@"🎉 Поздравляем {session.UserName} с успешным прохождением теста!

                {completionMessage}

                🎯 *{personalityType.FullName}*

                {personalityType.Description}

                ✨ *Сфера реализации:* {personalityType.Sphere}

                💪 *Сильные стороны:* {personalityType.Strengths}

                📋 *Рекомендации:* {personalityType.Recommendations}

                *Баллы по типам:*
                • Социальный: {result.Scores[1]}
                • Творческий: {result.Scores[2]}
                • Технический: {result.Scores[3]}
                • Аналитический: {result.Scores[4]}
                • Натуралистический: {result.Scores[5]}

                💡 Пройдите тест сами: https://t.me/{_botClient.GetMe(cancellationToken).Result.Username}";

                using var scope = _serviceProvider.CreateScope();
                var cardGenerator = scope.ServiceProvider.GetRequiredService<IStoryCardGenerator>();
                var cardImage = await cardGenerator.GenerateCardAsync(
                    result,
                    personalityType,
                    cancellationToken);

                var botInfo = await _botClient.GetMe(cancellationToken);
                var botUsername = botInfo.Username;

                var keyboard = new InlineKeyboardMarkup(new[]
                {
                    new[]
                    {
                        // Кнопка "Переслать" - открывает выбор чата
                        InlineKeyboardButton.WithSwitchInlineQueryCurrentChat(text: "📤 Переслать другу")
                    },
                    new[]
                    {
                        InlineKeyboardButton.WithUrl("🤖 Пройти тест", $"https://t.me/{botUsername}"),
                        InlineKeyboardButton.WithUrl("📢 Наш канал", "https://t.me/jsaland")
                    }
                });

                using var stream = new MemoryStream(cardImage);
                await _botClient.SendPhoto(
                    chatId: chatId,
                    photo: InputFile.FromStream(stream, "share_result.jpg"),
                    caption: shareText,
                    parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
                    replyMarkup: keyboard,
                    cancellationToken: cancellationToken);

                await _botClient.SendMessage(
                    chatId: chatId,
                    text: "💡 *Как поделиться:*\n\n" +
                         "1. Нажмите кнопку «📤 Переслать другу»\n" +
                         "2. Выберите чат или контакт\n" +
                         "3. Или удерживайте картинку → «Сохранить» → опубликовать в соцсетях\n\n" +
                         "Также можете скопировать текст выше и отправить вручную.",
                    parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
                    cancellationToken: cancellationToken);

                _loggerService.LogInfo($"Share card sent for session {session.Id}");
            }
            catch (Exception ex)
            {
                _loggerService.LogError(ex, $"Failed to generate share card for session {session.Id}");

                // Fallback: только текст
                await SendTextOnlyShareAsync(session, personalityType, chatId, cancellationToken);
            }
        }

        private async Task SendTextOnlyShareAsync(
            TestSession session,
            PersonalityType personalityType,
            long chatId,
            CancellationToken cancellationToken)
        {
            var result = await _testService.CalculateResultAsync(session);
            var completionMessage = await _configurationService.GetCompletionMessageAsync();
            var botInfo = await _botClient.GetMe(cancellationToken);

            var shareText = $@"🎉 Поздравляем {session.UserName} с успешным прохождением теста!

            {completionMessage}

            🎯 *{personalityType.FullName}*
            {personalityType.Description}

            *Баллы:*
            Социальный: {result.Scores[1]}, Творческий: {result.Scores[2]}, Технический: {result.Scores[3]}, Аналитический: {result.Scores[4]}, Натуралистический: {result.Scores[5]}

            💡 Пройди тест и узнай свой тип личности: https://t.me/{botInfo.Username}";

            var keyboard = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithSwitchInlineQueryCurrentChat(text: "📤 Поделиться с друзьями")
                },
                new[]
                {
                    InlineKeyboardButton.WithUrl("🤖 Начать тест", $"https://t.me/{botInfo.Username}?start=share_{session.Id}")
                }
            });

            await _botClient.SendMessage(
                chatId: chatId,
                text: shareText,
                parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
                replyMarkup: keyboard,
                cancellationToken: cancellationToken);
        }
    }
}