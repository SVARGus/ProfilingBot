
using ProfilingBot.Cloud.Handlers;
using ProfilingBot.Core.Interfaces;
using Telegram.Bot.Types;

namespace ProfilingBot.Cloud
{
    public class UpdateRouter
    {
        private readonly List<UpdateHandler> _handlers = new();

        private readonly ILoggerService _loggerService;

        public UpdateRouter(
            CommandUpdateHandler commandHandler,
            CallbackQueryUpdateHandler callbackHandler,
            TextMessageUpdateHandler textHandler,
            AdminCommandHandler adminHandler,
            ILoggerService loggerService)
        {
            _loggerService = loggerService;

            // РАЗМЕЩАЕМ В ПРАВИЛЬНОМ ПОРЯДКЕ!
            // 1. Админ команды (высший приоритет)
            _handlers.Add(adminHandler);

            // 2. Callback queries
            _handlers.Add(callbackHandler);

            // 3. Команды (кроме /admin)
            _handlers.Add(commandHandler);

            // 4. Текстовые сообщения (низший приоритет)
            _handlers.Add(textHandler);
        }

        public UpdateHandler? GetHandler(Update update)
        {
            // Ищем первый подходящий обработчик в порядке приоритета
            foreach (var handler in _handlers)
            {
                if (handler.CanHandle(update))
                {
                    _loggerService.LogDebug($"Selected handler: {handler.GetType().Name} for update {update.Id}");
                    return handler;
                }
            }

            _loggerService.LogDebug($"No handler found for update type: {update.Type}");
            return null;
        }
    }
}
