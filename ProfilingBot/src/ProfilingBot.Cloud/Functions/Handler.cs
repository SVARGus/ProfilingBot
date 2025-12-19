using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using ProfilingBot.Core.Interfaces;
using System.Text.Json;
using Telegram.Bot.Types;
using System.Text;

namespace ProfilingBot.Cloud.Functions
{
    /// <summary>
    /// Независимый обработчик для Yandex Cloud Functions
    /// Соответствует модели без наследования от YcFunction
    /// </summary>
    public class Handler
    {
        private readonly IServiceProvider _serviceProvider;

        // Константы для HTTP статусов
        private const int STATUS_OK = 200;
        private const int STATUS_BAD_REQUEST = 400;
        private const int STATUS_INTERNAL_ERROR = 500;

        public Handler()
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

        // Основной метод-обработчик для Yandex Cloud Functions
        // Важно: имя метода должно быть FunctionHandler
        public async Task<Response> FunctionHandler(string requestBody)
        {
            try
            {
                var update = JsonSerializer.Deserialize<Update>(requestBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                if (update == null)
                {
                    return new Response
                    {
                        StatusCode = STATUS_BAD_REQUEST,
                        Body = JsonSerializer.Serialize(new
                        {
                            error = "Invalid update format",
                            message = "Could not deserialize Telegram Update"
                        })
                    };
                }

                // Запускаем асинхронную обработку, но не ждем завершения
                // Для Telegram webhook важно быстро вернуть ответ
                _ = ProcessUpdateAsync(update);

                // Всегда возвращаем 200 OK для Telegram
                return new Response
                {
                    StatusCode = STATUS_OK,
                    Body = "OK"
                };
            }
            catch (JsonException jsonEx)
            {
                LogError($"JSON parsing error: {jsonEx.Message}");
                return new Response
                {
                    StatusCode = STATUS_BAD_REQUEST,
                    Body = JsonSerializer.Serialize(new { error = "Invalid JSON format" })
                };
            }
            catch (Exception ex)
            {
                LogError($"Unhandled exception: {ex}");
                return new Response
                {
                    StatusCode = STATUS_INTERNAL_ERROR,
                    Body = JsonSerializer.Serialize(new { error = "Internal server error" })
                };
            }
        }

        private async Task ProcessUpdateAsync(Update update)
        {
            using var scope = _serviceProvider.CreateScope();

            var chatId = update.Message?.Chat.Id ?? update.CallbackQuery?.Message?.Chat.Id;
            var logger = scope.ServiceProvider.GetRequiredService<ILoggerService>();
            logger.LogInfo($"Chat ID: {chatId}, Update ID: {update.Id}");

            try
            {
                // Режим продакшн - обычная обработка
                var router = scope.ServiceProvider.GetRequiredService<UpdateRouter>();
                var handler = router.GetHandler(update);

                if (handler != null)
                {
                    // Создаем CancellationToken
                    var cancellationToken = CancellationToken.None;
                    await handler.HandleAsync(update, cancellationToken);
                }
                else
                {
                    logger.LogWarning($"No handler found for update type: {update.Type}");
                }
            }
            catch (Exception ex)
            {
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

        private void LogError(string message)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var logger = scope.ServiceProvider.GetRequiredService<ILoggerService>();
                logger.LogError(message);
            }
            catch
            {
                Console.WriteLine($"ERROR: {message}");
            }
        }
    }
}

/// <summary>
/// Класс ответа для Yandex Cloud Functions
/// </summary>
public class Response
{
    public int StatusCode { get; set; }
    public string Body { get; set; } = string.Empty;
}