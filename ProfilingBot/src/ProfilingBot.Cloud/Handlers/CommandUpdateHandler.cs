using ProfilingBot.Core.Interfaces;
using Telegram.Bot;
using Telegram.Bot.Extensions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace ProfilingBot.Cloud.Handlers
{
    public class CommandUpdateHandler : UpdateHandler
    {

        public CommandUpdateHandler(
            TelegramBotClient botClient, 
            ITestService testService, 
            IConfigurationService configurationService, 
            ILoggerService loggerService) 
            : base(botClient, testService, configurationService, loggerService)
        {
        }

        public override bool CanHandle(Update update)
        {
            return update.Message?.Text?.StartsWith("/") == true;
        }

        public override async Task HandleAsync(Update update, CancellationToken cancellationToken)
        {
            var message = update.Message!;
            var command = message.Text!.Split(' ')[0].ToLowerInvariant();

            _loggerService.LogInfo($"Processing command '{command}' from user {message.From?.Id} (@{message.From?.Username})");

            switch (command)
            {
                case "/start":
                    await HandleStartCommandAsync (message, cancellationToken);
                    break;
                case "/help":
                    await HandleHelpCommandAsync(message, cancellationToken);
                    break;
                default:
                    await _botClient.SendMessage(
                        chatId: message.Chat.Id,
                        text: "Неизвестная команда. Используйте /start для начала теста.",
                        cancellationToken: cancellationToken);
                    break;
            }
        }

        private async Task HandleStartCommandAsync(Message message, CancellationToken cancellationToken)
        {
            var welcomeMessage = await _configurationService.GetWelcomeMessageAsync();

            var keyboard = new ReplyKeyboardMarkup(new[]
            {
            new[] { new KeyboardButton("Начать тест") }
        })
            {
                ResizeKeyboard = true,
                OneTimeKeyboard = true
            };

            await _botClient.SendMessage(
                chatId: message.Chat.Id,
                text: welcomeMessage,
                replyMarkup: keyboard,
                cancellationToken: cancellationToken);
        }

        private async Task HandleHelpCommandAsync(Message message, CancellationToken cancellationToken)
        {
            var helpText = "Привет! Я — бот для определения типа личности.\n\n" +
                          "Пройди тест из 8 вопросов и узнай свой тип!\n\n" +
                          "Доступные команды:\n" +
                          "/start — начать работу с ботом\n" +
                          "/help — показать эту справку\n" +
                          "/admin — админ-панель (для администраторов)\n\n" +
                          "Нажми кнопку «Начать тест» для старта.";

            await _botClient.SendMessage(
                chatId: message.Chat.Id,
                text: helpText,
                cancellationToken: cancellationToken);
        }
    }
}