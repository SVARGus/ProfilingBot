using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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

            // Регистрируем обработчики
            services.AddScoped<CommandUpdateHandler>();
            services.AddScoped<CallbackQueryUpdateHandler>();
            services.AddScoped<TextMessageUpdateHandler>();
            services.AddScoped<AdminCommandHandler>();

            // UpdateRouter с явными зависимостями
            services.AddScoped<UpdateRouter>(sp =>
            {
                var logger = sp.GetRequiredService<ILoggerService>();
                var commandHandler = sp.GetRequiredService<CommandUpdateHandler>();
                var callbackHandler = sp.GetRequiredService<CallbackQueryUpdateHandler>();
                var textHandler = sp.GetRequiredService<TextMessageUpdateHandler>();
                var adminHandler = sp.GetRequiredService<AdminCommandHandler>();

                return new UpdateRouter(
                    commandHandler,
                    callbackHandler,
                    textHandler,
                    adminHandler,
                    logger);
            });

            return services;
        }

        public static IServiceCollection AddCoreServices(this IServiceCollection services, string configPath, string dataPath)
        {
            // Логгер
            services.AddSingleton<ILoggerService>(sp => new FileLoggerService(Path.Combine(dataPath, "logs")));


            // Хранилище
            services.AddSingleton<IStorageService>(sp =>
            {
                var logger = sp.GetRequiredService<ILoggerService>();
                return new FileStorageService(dataPath, logger);
            });

            // Генератор карточек - Singleton с IServiceProvider
            services.AddSingleton<IStoryCardGenerator>(sp =>
            {
                var logger = sp.GetRequiredService<ILoggerService>();
                return new StoryCardGenerator(logger, sp); // Передаем сам IServiceProvider
            });

            // Сервис экспорта
            services.AddSingleton<IExportService, ExcelExportService>();

            // Конфигурация (оставляем Scoped, т.к. может кэшировать)
            services.AddScoped<IConfigurationService>(sp =>
            {
                var logger = sp.GetRequiredService<ILoggerService>();
                return new FileConfigurationService(configPath, logger);
            });

            // Сервисы бизнес-логики
            services.AddScoped<ITestService, TestService>();
            services.AddScoped<IAdminService, AdminService>();
            services.AddScoped<IResultGeneratorService, ResultGeneratorService>();

            return services;
        }
    }
}
