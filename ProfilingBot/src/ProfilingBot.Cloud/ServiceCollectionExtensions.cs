using Microsoft.Extensions.DependencyInjection;
using ProfilingBot.Cloud.Handlers;
using ProfilingBot.Core.Interfaces;
using ProfilingBot.Core.Services;
using Telegram.Bot;

namespace ProfilingBot.Cloud
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddTelegramBot(this IServiceCollection services, string botToken)
        {
            // Создаем экземпляр TelegramBotClient
            var botClient = new TelegramBotClient(botToken);

            // Регистрируем как ITelegramBot
            services.AddSingleton<ITelegramBotClient>(botClient);

            // Также регистрируем TelegramBotClient для обратной совместимости
            services.AddSingleton(botClient);

            services.AddScoped<UpdateRouter>();
            services.AddScoped<UpdateHandler, CommandUpdateHandler>();
            services.AddScoped<UpdateHandler, CallbackQueryUpdateHandler>();
            services.AddScoped<UpdateHandler, TextMessageUpdateHandler>();

            return services;
        }

        public static IServiceCollection AddCoreServices(this IServiceCollection services, string configPath, string dataPath)
        {
            // Логгер
            services.AddSingleton<ILoggerService>(sp =>
                new FileLoggerService(Path.Combine(dataPath, "logs")));

            // Конфигурация
            services.AddSingleton<IConfigurationService>(sp =>
            {
                var logger = sp.GetRequiredService<ILoggerService>();
                return new FileConfigurationService(configPath, logger);
            });

            // Хранилище
            services.AddSingleton<IStorageService>(sp =>
            {
                var logger = sp.GetRequiredService<ILoggerService>();
                return new FileStorageService(dataPath, logger);
            });

            // Сервисы бизнес-логики
            services.AddScoped<ITestService, TestService>();
            services.AddScoped<IResultGeneratorService, ResultGeneratorService>();

            return services;
        }
    }
}
