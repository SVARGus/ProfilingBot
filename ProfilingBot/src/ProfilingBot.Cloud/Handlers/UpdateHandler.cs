using ProfilingBot.Core.Interfaces;
using ProfilingBot.Core.Models;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace ProfilingBot.Cloud.Handlers
{
    public abstract class UpdateHandler
    {
        protected readonly TelegramBotClient _botClient;
        protected readonly ITestService _testService;
        protected readonly IConfigurationService _configurationService;
        protected readonly ILoggerService _loggerService;

        protected UpdateHandler(
            TelegramBotClient botClient, 
            ITestService testService, 
            IConfigurationService configurationService, 
            ILoggerService loggerService)
        {
            _botClient = botClient;
            _testService = testService;
            _configurationService = configurationService;
            _loggerService = loggerService;
        }

        // Может ли этот обработчик обработать данное обновление
        public abstract bool CanHandle(Update update);

        // Основной метод обработки
        public abstract Task HandleAsync(Update update, CancellationToken cancellationToken);

        // === ОБЩИЕ МЕТОДЫ ДЛЯ ВСЕХ ОБРАБОТЧИКОВ ===

        protected async Task SendQuestionAsync(TestSession session, Question question, long chatId, CancellationToken cancellationToken)
        {
            var config = await _configurationService.GetBotConfigAsync();
            var questionNumber = session.CurrentQuestionIndex;
            var totalQuestions = config.TotalQuestions;

            var messageText = $"*Вопрос {questionNumber} из {totalQuestions}*\n\n{question.Text}";

            // Создаем инлайн-кнопки для каждого варианта ответа
            var buttons = new List<InlineKeyboardButton[]>();
            foreach (var answer in question.Answers)
            {
                var button = InlineKeyboardButton.WithCallbackData(
                    text: $"🔹 {answer.Text}",
                    callbackData: $"answer_{session.Id}_{question.Id}_{answer.Id}");
                buttons.Add(new[] { button });
            }

            var keyboard = new InlineKeyboardMarkup(buttons);

            await _botClient.SendMessage(
                chatId: chatId,
                text: messageText,
                parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
                replyMarkup: keyboard,
                cancellationToken: cancellationToken);
        }

        protected async Task ShowTestResultAsync(TestSession session, long chatId, CancellationToken cancellationToken)
        {
            try
            {
                // Рассчитываем результат
                var result = await _testService.CalculateResultAsync(session);

                // Получаем тип личности
                var personalityType = await _configurationService.GetPersonalityTypeAsync(result.PersonalityTypeId);

                if (personalityType == null)
                {
                    await _botClient.SendMessage(
                        chatId: chatId,
                        text: "Не удалось определить тип личности. Пожалуйста, попробуйте позже.",
                        cancellationToken: cancellationToken);
                    return;
                }

                // Формируем сообщение с результатом
                var completionMessage = await _configurationService.GetCompletionMessageAsync();
                var resultText = $@"{completionMessage}

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
                    • Натуралистический: {result.Scores[5]}";

                // Кнопки для действий с результатом
                var buttons = new[]
                {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("📱 Получить карточку", $"getcard_{session.Id}"),
                    InlineKeyboardButton.WithCallbackData("📤 Поделиться", $"share_{session.Id}")
                },
                new[]
                {
                    InlineKeyboardButton.WithUrl("📢 Канал проекта", "t.me/jsland")
                }
            };

                var keyboard = new InlineKeyboardMarkup(buttons);

                await _botClient.SendMessage(
                    chatId: chatId,
                    text: resultText,
                    parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
                    replyMarkup: keyboard,
                    cancellationToken: cancellationToken);

                _loggerService.LogInfo($"Test result shown for session {session.Id}, type: {personalityType.Name}");
            }
            catch (Exception ex)
            {
                _loggerService.LogError(ex, $"Failed to show test result for session {session.Id}");

                await _botClient.SendMessage(
                    chatId: chatId,
                    text: "Произошла ошибка при формировании результатов. Пожалуйста, попробуйте позже.",
                    cancellationToken: cancellationToken);
            }
        }

        protected string GetUserName(User user)
        {
            if (!string.IsNullOrEmpty(user.Username))
                return $"@{user.Username}";

            if (!string.IsNullOrEmpty(user.FirstName))
                return user.FirstName;

            return $"User_{user.Id}";
        }
    }
}