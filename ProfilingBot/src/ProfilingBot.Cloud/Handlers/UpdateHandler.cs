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

            // ========== ДОБАВЛЕНО: Получение порядка вариантов для этого вопроса ==========
            if (!session.AnswerOrder.TryGetValue(question.Id, out var answerOrder))
            {
                // На всякий случай, если порядок не найден, используем оригинальный
                answerOrder = question.Answers.Select(a => a.Id).ToList();
                _loggerService.LogWarning($"Answer order not found for question {question.Id}, using default");
            }

            // Получаем варианты в правильном порядке для этой сессии
            var orderedAnswers = answerOrder
                .Select(answerId => question.Answers.First(a => a.Id == answerId))
                .ToList();
            // ==============================================================================

            // ========== ДОБАВЛЕНО: Новый формат сообщения ==========
            var progressBar = GetProgressBar(questionNumber, totalQuestions);

            var messageText = $"*Вопрос {questionNumber}*\n\n" +
                              $"\"{question.Text}\"\n\n" +
                              string.Join("\n", orderedAnswers.Select((a, i) => $"*Вариант {i + 1}:* {a.Text}\n"));
            // ======================================================

            // Создаем инлайн-кнопки для каждого варианта ответа
            var buttons = new List<InlineKeyboardButton[]>();

            // Массив эмодзи для вариантов
            var optionEmojis = new[] { "1️⃣", "2️⃣", "3️⃣", "4️⃣", "5️⃣" };

            for (int i = 0; i < orderedAnswers.Count; i++)
            {
                // Только sessionId и displayIndex (1-5)
                var button = InlineKeyboardButton.WithCallbackData(
                    text: $"{optionEmojis[i]} Вариант {i + 1}", // "1️⃣ Вариант 1"
                    callbackData: $"answer_{session.Id}_{i + 1}"); // Упрощено!
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
                    InlineKeyboardButton.WithUrl("📢 Канал проекта", "t.me/jsaland"),
                    InlineKeyboardButton.WithUrl("✍️ Обратиться к специалисту", "t.me/SalandJulia")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("🔄 Пройти тест заново", $"starttest_{session.Id}")
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

        private string GetProgressBar(int current, int total)
        {
            var filled = "█";
            var empty = "░";
            var width = 10;

            var filledCount = (int)Math.Round((double)current / total * width);
            var emptyCount = width - filledCount;

            return $"[{new string(filled[0], filledCount)}{new string(empty[0], emptyCount)}]";
        }
    }
}