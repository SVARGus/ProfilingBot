using Microsoft.AspNetCore.Mvc;
using ProfilingBot.Cloud;
using Telegram.Bot.Types;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ProfilingBot.Api.Controllers
{
    [ApiController]
    [Route("api/bot")] // Базовый путь
    public class BotController : ControllerBase
    {
        private readonly UpdateRouter _router;
        private readonly ILogger<BotController> _logger;

        public BotController(UpdateRouter router, ILogger<BotController> logger)
        {
            _router = router;
            _logger = logger;
        }

        [HttpPost("webhook")] // Полный путь: POST /api/bot/webhook
        public async Task<IActionResult> PostWebhook([FromBody] Update update)
        {
            if (update == null)
            {
                _logger.LogWarning("Received null update");
                return BadRequest("Invalid update format");
            }

            _logger.LogInformation(
                "Received update {UpdateId} from user {UserId}",
                update.Id,
                update.Message?.From?.Id ?? update.CallbackQuery?.From?.Id);

            // Асинхронная обработка без ожидания(как в Cloud Functions)
            _ = ProcessUpdateAsync(update);

            // Всегда возвращаем 200 OK для Telegram
            return Ok("OK");
        }

        private async Task ProcessUpdateAsync(Update update)
        {
            try
            {
                var handler = _router.GetHandler(update);
                if (handler != null)
                {
                    await handler.HandleAsync(update, CancellationToken.None);
                }
                else
                {
                    _logger.LogWarning("No handler found for update type {UpdateType}", update.Type);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing update {UpdateId}", update.Id);
            }
        }
    }
}
