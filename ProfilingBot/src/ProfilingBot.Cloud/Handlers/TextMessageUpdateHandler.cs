using ProfilingBot.Core.Interfaces;
using ProfilingBot.Core.Models;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace ProfilingBot.Cloud.Handlers
{
    public class TextMessageUpdateHandler : UpdateHandler
    {
        public TextMessageUpdateHandler(
            TelegramBotClient botClient,
            ITestService testService,
            IConfigurationService configurationService,
            ILoggerService loggerService)
            : base(botClient, testService, configurationService, loggerService)
        {
        }

        public override bool CanHandle(Update update)
        {
            // Обрабатываем только текстовые сообщения НЕ начинающиеся с '/'
            return update.Message?.Text != null && !update.Message.Text.StartsWith('/');
        }

        public override async Task HandleAsync(Update update, CancellationToken cancellationToken)
        {
            var message = update.Message!;
            var text = message.Text!.Trim();
            var userId = message.From!.Id;
            var chatId = message.Chat.Id;

            _loggerService.LogInfo($"Processing text message from user {userId}: '{text}'");

            // Обработка кнопки "Начать тест" (регистронезависимо)
            if (text.Equals("Начать тест", StringComparison.OrdinalIgnoreCase))
            {
                await HandleStartTestButtonAsync(userId, GetUserName(message.From), chatId, cancellationToken);
            }
            else
            {
                // Если получен неизвестный текст, показываем подсказку
                await HandleUnknownTextAsync(chatId, cancellationToken);
            }
        }

        private async Task HandleStartTestButtonAsync(long userId, string userName, long chatId, CancellationToken cancellationToken)
        {
            try
            {
                _loggerService.LogInfo($"User {userId} ({userName}) clicked 'Начать тест'");

                // Проверяем, есть ли уже активная сессия
                var existingSession = await _testService.GetActiveSessionAsync(userId);
                if (existingSession != null)
                {
                    _loggerService.LogWarning($"User {userId} already has active session {existingSession.Id}");

                    // Предлагаем продолжить или начать заново
                    await AskToContinueOrRestartAsync(existingSession, chatId, cancellationToken);
                    return;
                }

                // Создаем новую сессию
                var session = await _testService.StartTestAsync(userId, userName);

                // Получаем вступительное сообщение
                var introMessage = await _configurationService.GetIntroMessageAsync();

                // Отправляем вступительное сообщение
                await _botClient.SendMessage(
                    chatId: chatId,
                    text: introMessage,
                    cancellationToken: cancellationToken);

                // Небольшая пауза для лучшего UX
                await Task.Delay(500, cancellationToken);

                // Показываем первый вопрос 
                var question = await _testService.GetCurrentQuestionAsync(session.Id);
                if (question != null)
                {
                    await SendQuestionAsync(session, question, chatId, cancellationToken);
                }

                _loggerService.LogInfo($"Test started for user {userId}, session {session.Id}");
            }
            catch (Exception ex)
            {
                _loggerService.LogError(ex, $"Failed to start test for user {userId}");

                await _botClient.SendMessage(
                    chatId: chatId,
                    text: "❌ Произошла ошибка при запуске теста. Пожалуйста, попробуйте позже или обратитесь к администратору.",
                    cancellationToken: cancellationToken);
            }
        }

        private async Task AskToContinueOrRestartAsync(TestSession existingSession, long chatId, CancellationToken cancellationToken)
        {
            var messageText = $"📝 У вас уже есть начатый тест.\n\n" +
                             $"Вы начали его {existingSession.StartedAt:dd.MM.yyyy HH:mm}.\n" +
                             $"Прогресс: {existingSession.CurrentQuestionIndex - 1}/{existingSession.Answers.Count} вопросов пройдено.\n\n" +
                             $"Что хотите сделать?";

            var buttons = new[]
            {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("▶️ Продолжить тест", $"continue_{existingSession.Id}"),
                InlineKeyboardButton.WithCallbackData("🔄 Начать заново", $"restart_{existingSession.Id}")
            }
        };

            var keyboard = new InlineKeyboardMarkup(buttons);

            await _botClient.SendMessage(
                chatId: chatId,
                text: messageText,
                replyMarkup: keyboard,
                cancellationToken: cancellationToken);
        }

        private async Task HandleUnknownTextAsync(long chatId, CancellationToken cancellationToken)
        {
            var helpText = "🤖 Я понимаю только команды и кнопки!\n\n" +
                          "• Используйте /start для начала работы\n" +
                          "• Нажмите кнопку \"Начать тест\" для старта тестирования\n" +
                          "• Используйте /help для справки";

            await _botClient.SendMessage(
                chatId: chatId,
                text: helpText,
                cancellationToken: cancellationToken);
        }
    }
}