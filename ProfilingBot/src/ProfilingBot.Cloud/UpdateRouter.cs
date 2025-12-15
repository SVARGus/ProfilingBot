
using ProfilingBot.Cloud.Handlers;
using Telegram.Bot.Types;

namespace ProfilingBot.Cloud
{
    public class UpdateRouter
    {
        private readonly IEnumerable<UpdateHandler> _handlers;

        public UpdateRouter(IEnumerable<UpdateHandler> handlers)
        {
            _handlers = handlers;
        }

        public UpdateHandler? GetHandler(Update update)
        {
            return _handlers.FirstOrDefault(handler => handler.CanHandle(update));
        }
    }
}
