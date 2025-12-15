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

            _loggerService.LogInfo($"Processing command '{command}' from user {message.From?.Id}");

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
            var helpText = @"Это бот для тестирования личности. 

                Доступные команды:
                /start - начать тест
                /help - показать эту справку

                Просто нажмите кнопку 'Начать тест' после команды /start.";

            await _botClient.SendMessage(
                chatId: message.Chat.Id,
                text: helpText,
                cancellationToken: cancellationToken);
        }
    }
}