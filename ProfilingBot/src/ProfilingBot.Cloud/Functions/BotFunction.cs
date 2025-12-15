using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using ProfilingBot.Core.Interfaces;
using System.Text.Json;
using Telegram.Bot.Types;
using Yandex.Cloud.Functions;

namespace ProfilingBot.Cloud.Functions
{
    public class BotFunction : YcFunction<string, IActionResult>
    {
        private readonly IServiceProvider _serviceProvider;
        public BotFunction()
        {
            // Получаем конфигурацию из переменных окружения
            var botToken = Environment.GetEnvironmentVariable("BOT_TOKEN")
                ?? throw new InvalidOperationException("BOT_TOKEN is not configured");

            var configPath = Environment.GetEnvironmentVariable("CONFIG_PATH")
                ?? "/app/config";

            var dataPath = Environment.GetEnvironmentVariable("DATA_PATH")
                ?? "/app/data";

            // Настраиваем DI-контейнер
            var services = new ServiceCollection();

            services.AddCoreServices(configPath, dataPath);
            services.AddTelegramBot(botToken);

            _serviceProvider = services.BuildServiceProvider();

            // Инициализируем хранилище
            InitializeStorageAsync().Wait();
        }

        // Убираем async и изменяем возвращаемый тип
        public IActionResult FunctionHandler(string request, Context context)
        {
            try
            {
                var update = JsonSerializer.Deserialize<Update>(request, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                if (update == null)
                {
                    return new BadRequestObjectResult(new
                    {
                        error = "Invalid update format",
                        requestId = context.RequestId
                    });
                }

                // Запускаем асинхронную обработку, но не ждем завершения
                // Для Telegram webhook важно быстро вернуть ответ
                _ = ProcessUpdateAsync(update, context);

                // Всегда возвращаем 200 OK для Telegram
                return new OkResult();
            }
            catch (JsonException jsonEx)
            {
                LogError($"JSON parsing error: {jsonEx.Message}", context);
                return new BadRequestObjectResult(new { error = "Invalid JSON format" });
            }
            catch (Exception ex)
            {
                LogError($"Unhandled exception: {ex}", context);
                return new StatusCodeResult(500);
            }
        }

        private async Task ProcessUpdateAsync(Update update, Context context)
        {
            using var scope = _serviceProvider.CreateScope();

            try
            {
                var router = scope.ServiceProvider.GetRequiredService<UpdateRouter>();
                var handler = router.GetHandler(update);

                if (handler != null)
                {
                    // Создаем CancellationToken (его нет в Context)
                    var cancellationToken = CancellationToken.None;
                    await handler.HandleAsync(update, cancellationToken);
                }
                else
                {
                    var logger = scope.ServiceProvider.GetRequiredService<ILoggerService>();
                    logger.LogWarning($"No handler found for update type: {update.Type}");
                }
            }
            catch (Exception ex)
            {
                var logger = scope.ServiceProvider.GetRequiredService<ILoggerService>();
                logger.LogError(ex, $"Error processing update {update.Id}");
            }
        }

        private async Task InitializeStorageAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var storageService = scope.ServiceProvider.GetRequiredService<IStorageService>();
            await storageService.InitializeAsync();

            var logger = scope.ServiceProvider.GetRequiredService<ILoggerService>();
            logger.LogInfo("Storage initialized successfully");
        }

        private void LogError(string message, Context context)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var logger = scope.ServiceProvider.GetRequiredService<ILoggerService>();
                logger.LogError($"[RequestId: {context.RequestId}] {message}");
            }
            catch
            {
                Console.WriteLine($"[RequestId: {context.RequestId}] ERROR: {message}");
            }
        }
    }
}
