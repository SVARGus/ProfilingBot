using ProfilingBot.Core.Interfaces;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace ProfilingBot.Cloud.Handlers
{
    public class AdminCommandHandler : UpdateHandler
    {
        private readonly IAdminService _adminService;
        private readonly IStorageService _storageService;
        private readonly IExportService _exportService;

        public AdminCommandHandler(
            TelegramBotClient botClient,
            ITestService testService,
            IConfigurationService configurationService,
            ILoggerService loggerService,
            IAdminService adminService,
            IStorageService storageService,
            IExportService exportService)
            : base(botClient, testService, configurationService, loggerService)
        {
            _adminService = adminService;
            _storageService = storageService;
            _exportService = exportService;
        }

        public override bool CanHandle(Update update)
        {
            // Команда /admin или сообщение "Админ-панель"
            return update.Message?.Text?.Equals("/admin", StringComparison.OrdinalIgnoreCase) == true ||
                   update.Message?.Text?.Equals("Админ-панель", StringComparison.OrdinalIgnoreCase) == true ||
                   update.CallbackQuery?.Data?.StartsWith("admin_") == true;
        }

        public override async Task HandleAsync(Update update, CancellationToken cancellationToken)
        {
            var userId = update.Message?.From?.Id ?? update.CallbackQuery?.From?.Id;
            var chatId = update.Message?.Chat.Id ?? update.CallbackQuery?.Message?.Chat.Id;

            if (!userId.HasValue || !chatId.HasValue)
            {
                _loggerService.LogWarning("Admin command without user/chat ID");
                return;
            }

            // Проверяем права доступа
            if (!await _adminService.IsAdminAsync(userId.Value))
            {
                await HandleNonAdminAccess(userId.Value, chatId.Value, cancellationToken);
                return;
            }

            _loggerService.LogInfo($"Admin command from user {userId}");

            try
            {
                if (update.CallbackQuery != null)
                {
                    await HandleAdminCallbackAsync(update.CallbackQuery, cancellationToken);
                }
                else
                {
                    await ShowAdminMenuAsync(chatId.Value, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _loggerService.LogError(ex, $"Error handling admin command from user {userId}");
                await SendErrorMessageAsync(chatId.Value, cancellationToken);
            }
        }

        private async Task HandleNonAdminAccess(long userId, long chatId, CancellationToken cancellationToken)
        {
            _loggerService.LogWarning($"Non-admin user {userId} attempted to access admin panel");

            await _botClient.SendMessage(
                chatId: chatId,
                text: "⛔ У вас нет доступа к администраторским функциям.\n\n" +
                      "Для доступа обратитесь к администратору бота.",
                cancellationToken: cancellationToken);
        }

        private async Task ShowAdminMenuAsync(long chatId, CancellationToken cancellationToken)
        {
            var isOwner = await _adminService.IsOwnerAsync(chatId);
            var roleText = isOwner ? "👑 Владелец" : "🛠️ Администратор";

            var menuText = $"{roleText} *Админ-панель*\n\n" +
                          "Выберите действие:";

            var buttons = new List<InlineKeyboardButton[]>
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("📊 Статистика за сегодня", "admin_stats_today"),
                    InlineKeyboardButton.WithCallbackData("📈 Статистика за 7 дней", "admin_stats_week")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("📥 Excel отчет (все время)", "admin_export_all"),
                    InlineKeyboardButton.WithCallbackData("📅 Excel (30 дней)", "admin_export_30d")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("ℹ️ Информация о боте", "admin_bot_info"),
                    InlineKeyboardButton.WithCallbackData("🔄 Обновить конфигурацию", "admin_reload_config")
                }
            };

            // Кнопки управления админами только для владельца
            if (isOwner)
            {
                buttons.Add(new[]
                {
                    InlineKeyboardButton.WithCallbackData("👥 Список админов", "admin_list_admins"),
                    InlineKeyboardButton.WithCallbackData("➕ Добавить админа", "admin_add_admin")
                });
            }

            var keyboard = new InlineKeyboardMarkup(buttons);

            await _botClient.SendMessage(
                chatId: chatId,
                text: menuText,
                parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
                replyMarkup: keyboard,
                cancellationToken: cancellationToken);
        }

        private async Task HandleAdminCallbackAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
        {
            var chatId = callbackQuery.Message!.Chat.Id;
            var userId = callbackQuery.From.Id;

            _loggerService.LogDebug($"Admin callback: {callbackQuery.Data} from user {userId}");

            try
            {
                switch (callbackQuery.Data)
                {
                    case "admin_stats_today":
                        await SendDailyStatsAsync(chatId, cancellationToken);
                        break;

                    case "admin_stats_week":
                        await SendWeeklyStatsAsync(chatId, cancellationToken);
                        break;

                    case "admin_export_all":
                        await SendExcelReportAsync(chatId, null, null, "за все время", cancellationToken);
                        break;

                    case "admin_export_30d":
                        var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
                        await SendExcelReportAsync(chatId, thirtyDaysAgo, DateTime.UtcNow, "за 30 дней", cancellationToken);
                        break;

                    case "admin_bot_info":
                        await SendBotInfoAsync(chatId, cancellationToken);
                        break;

                    case "admin_reload_config":
                        await ReloadConfigurationAsync(chatId, cancellationToken);
                        break;

                    case "admin_list_admins":
                        await ListAdminsAsync(chatId, cancellationToken);
                        break;

                    case "admin_add_admin":
                        await PromptAddAdminAsync(chatId, cancellationToken);
                        break;

                    case "admin_back":
                        await ShowAdminMenuAsync(chatId, cancellationToken);
                        break;
                }
            }
            catch (Exception ex)
            {
                _loggerService.LogError(ex, $"Error handling admin callback: {callbackQuery.Data}");
                await SendErrorMessageAsync(chatId, cancellationToken);
            }
            finally
            {
                // Всегда отвечаем на callback
                await _botClient.AnswerCallbackQuery(
                    callbackQueryId: callbackQuery.Id,
                    cancellationToken: cancellationToken);
            }
        }

        private async Task SendDailyStatsAsync(long chatId, CancellationToken cancellationToken)
        {
            await _botClient.SendMessage(
                chatId: chatId,
                text: "📊 Загружаю статистику за сегодня...",
                cancellationToken: cancellationToken);

            try
            {
                var stats = await _adminService.GetDailyStatsAsync(DateTime.UtcNow.Date);

                var message = new StringBuilder();
                message.AppendLine($"📊 *Статистика за {DateTime.UtcNow:dd.MM.yyyy}*\n");
                message.AppendLine($"✅ Завершено тестов: *{stats.TotalTestsCompleted}*");
                message.AppendLine($"👥 Уникальных пользователей: *{stats.TotalUniqueUsers}*");
                message.AppendLine($"⏱️ Среднее время теста: *{stats.AverageTestDuration:mm\\:ss}*");

                if (stats.AbandonedTests > 0)
                {
                    message.AppendLine($"🚫 Не завершено тестов: *{stats.AbandonedTests}*");
                }

                message.AppendLine($"\n🏆 *Самый популярный тип:* {stats.MostPopularPersonalityType}");

                if (stats.PersonalityTypeDistribution.Any())
                {
                    message.AppendLine($"\n📈 *Распределение типов:*");
                    foreach (var kvp in stats.PersonalityTypeDistribution.OrderByDescending(x => x.Value))
                    {
                        var percentage = stats.TotalTestsCompleted > 0
                            ? (kvp.Value * 100.0 / stats.TotalTestsCompleted).ToString("0.0")
                            : "0";
                        message.AppendLine($"{kvp.Key}: {kvp.Value} ({percentage}%)");
                    }
                }

                var keyboard = new InlineKeyboardMarkup(new[]
                {
                    new[] { InlineKeyboardButton.WithCallbackData("⬅️ Назад", "admin_back") }
                });

                await _botClient.SendMessage(
                    chatId: chatId,
                    text: message.ToString(),
                    parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
                    replyMarkup: keyboard,
                    cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                _loggerService.LogError(ex, "Failed to get daily stats");
                await _botClient.SendMessage(
                    chatId: chatId,
                    text: "❌ Не удалось загрузить статистику. Попробуйте позже.",
                    cancellationToken: cancellationToken);
            }
        }

        private async Task SendWeeklyStatsAsync(long chatId, CancellationToken cancellationToken)
        {
            await _botClient.SendMessage(
                chatId: chatId,
                text: "📈 Загружаю статистику за 7 дней...",
                cancellationToken: cancellationToken);

            try
            {
                var startDate = DateTime.UtcNow.Date.AddDays(-6); // Последние 7 дней включая сегодня
                var stats = await _adminService.GetWeeklyStatsAsync(startDate);

                var message = new StringBuilder();
                message.AppendLine($"📈 *Статистика за неделю*");
                message.AppendLine($"{startDate:dd.MM.yyyy} - {DateTime.UtcNow:dd.MM.yyyy}\n");
                message.AppendLine($"✅ Всего тестов: *{stats.TotalTestsCompleted}*");
                message.AppendLine($"👥 Уникальных пользователей: *{stats.TotalUniqueUsers}*");
                message.AppendLine($"⏱️ Среднее время теста: *{stats.AverageTestDuration:mm\\:ss}*");
                message.AppendLine($"🏆 Самый популярный тип: *{stats.MostPopularPersonalityType}*");

                if (stats.DailyCompletionCount.Any())
                {
                    message.AppendLine($"\n📅 *Тестов по дням:*");
                    foreach (var kvp in stats.DailyCompletionCount.OrderBy(x => x.Key))
                    {
                        message.AppendLine($"{kvp.Key:dd.MM}: {kvp.Value} тест(ов)");
                    }
                }

                var keyboard = new InlineKeyboardMarkup(new[]
                {
                    new[] { InlineKeyboardButton.WithCallbackData("⬅️ Назад", "admin_back") }
                });

                await _botClient.SendMessage(
                    chatId: chatId,
                    text: message.ToString(),
                    parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
                    replyMarkup: keyboard,
                    cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                _loggerService.LogError(ex, "Failed to get weekly stats");
                await _botClient.SendMessage(
                    chatId: chatId,
                    text: "❌ Не удалось загрузить статистику. Попробуйте позже.",
                    cancellationToken: cancellationToken);
            }
        }

        private async Task SendExcelReportAsync(
            long chatId,
            DateTime? from,
            DateTime? to,
            string periodDescription,
            CancellationToken cancellationToken)
        {
            var loadingMessage = await _botClient.SendMessage(
                chatId: chatId,
                text: $"📊 Генерирую Excel отчет {periodDescription}...",
                cancellationToken: cancellationToken);

            try
            {
                var excelData = await _adminService.ExportToExcelAsync(from, to);

                if (excelData == null || excelData.Length == 0)
                {
                    await _botClient.EditMessageText(
                        chatId: chatId,
                        messageId: loadingMessage.MessageId,
                        text: $"📭 Нет данных для отчета {periodDescription}",
                        cancellationToken: cancellationToken);
                    return;
                }

                using var stream = new MemoryStream(excelData);
                var fileName = $"report_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";

                await _botClient.SendDocument(
                    chatId: chatId,
                    document: InputFile.FromStream(stream, fileName),
                    caption: $"📋 Отчет {periodDescription}\n" +
                            $"📅 Сгенерировано: {DateTime.UtcNow:dd.MM.yyyy HH:mm}",
                    cancellationToken: cancellationToken);

                // Удаляем сообщение "Генерирую..."
                await _botClient.DeleteMessage(chatId, loadingMessage.MessageId, cancellationToken);
            }
            catch (Exception ex)
            {
                _loggerService.LogError(ex, $"Failed to generate Excel report {periodDescription}");

                await _botClient.EditMessageText(
                    chatId: chatId,
                    messageId: loadingMessage.MessageId,
                    text: $"❌ Ошибка при генерации отчета {periodDescription}",
                    cancellationToken: cancellationToken);
            }
        }

        private async Task SendBotInfoAsync(long chatId, CancellationToken cancellationToken)
        {
            var botInfo = await _botClient.GetMe();
            var config = await _configurationService.GetBotConfigAsync();
            var totalTests = await _storageService.GetCompletedSessionsCountAsync();
            var activeSessions = (await _storageService.GetAllActiveSessionsAsync()).Count;

            var message = $"🤖 *Информация о боте*\n\n" +
                         $"Имя: {botInfo.FirstName}\n" +
                         $"Username: @{botInfo.Username}\n" +
                         $"ID: {botInfo.Id}\n\n" +
                         $"📊 *Статистика:*\n" +
                         $"Всего тестов: {totalTests}\n" +
                         $"Активных сессий: {activeSessions}\n\n" +
                         $"⚙️ *Конфигурация:*\n" +
                         $"Вопросов в тесте: {config.TotalQuestions}\n" +
                         $"Вариантов ответа: {config.AnswersPerQuestion}\n\n" +
                         $"🔄 Последнее обновление: {DateTime.UtcNow:dd.MM.yyyy HH:mm}";

            var keyboard = new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("⬅️ Назад", "admin_back") }
            });

            await _botClient.SendMessage(
                chatId: chatId,
                text: message,
                parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
                replyMarkup: keyboard,
                cancellationToken: cancellationToken);
        }

        private async Task ReloadConfigurationAsync(long chatId, CancellationToken cancellationToken)
        {
            // TODO: Реализовать метод в IConfigurationService
            await _botClient.SendMessage(
                chatId: chatId,
                text: "🔄 Эта функция находится в разработке\n\n" +
                      "Сейчас для обновления конфигурации требуется перезапуск бота.",
                cancellationToken: cancellationToken);
        }

        private async Task ListAdminsAsync(long chatId, CancellationToken cancellationToken)
        {
            if (!await _adminService.CanManageAdminsAsync(chatId))
            {
                await _botClient.SendMessage(
                    chatId: chatId,
                    text: "⛔ У вас нет прав для управления администраторами.",
                    cancellationToken: cancellationToken);
                return;
            }

            try
            {
                var admins = await _adminService.GetAdminsAsync();

                var message = new StringBuilder();
                message.AppendLine("👥 *Список администраторов*\n");

                foreach (var admin in admins)
                {
                    var roleIcon = admin.Role == "owner" ? "👑" : "🛠️";
                    message.AppendLine($"{roleIcon} {admin.UserName} (ID: {admin.UserId})");
                    message.AppendLine($"Роль: {admin.Role}");
                    message.AppendLine($"Добавлен: {admin.AddedAt:dd.MM.yyyy}");
                    message.AppendLine($"Добавил: {admin.AddedBy}\n");
                }

                var keyboard = new InlineKeyboardMarkup(new[]
                {
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData("➕ Добавить админа", "admin_add_admin"),
                        InlineKeyboardButton.WithCallbackData("⬅️ Назад", "admin_back")
                    }
                });

                await _botClient.SendMessage(
                    chatId: chatId,
                    text: message.ToString(),
                    parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
                    replyMarkup: keyboard,
                    cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                _loggerService.LogError(ex, "Failed to list admins");
                await _botClient.SendMessage(
                    chatId: chatId,
                    text: "❌ Не удалось загрузить список администраторов.",
                    cancellationToken: cancellationToken);
            }
        }

        private async Task PromptAddAdminAsync(long chatId, CancellationToken cancellationToken)
        {
            if (!await _adminService.CanManageAdminsAsync(chatId))
            {
                await _botClient.SendMessage(
                    chatId: chatId,
                    text: "⛔ У вас нет прав для добавления администраторов.",
                    cancellationToken: cancellationToken);
                return;
            }

            await _botClient.SendMessage(
                chatId: chatId,
                text: "➕ *Добавление администратора*\n\n" +
                      "Отправьте username нового администратора (например, @username).\n" +
                      "Или отправьте /cancel для отмены.",
                parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
                cancellationToken: cancellationToken);

            // TODO: Реализовать состояние ожидания username
            // Можно использовать FSM (Finite State Machine) или просто ждать следующее сообщение
        }

        private async Task SendErrorMessageAsync(long chatId, CancellationToken cancellationToken)
        {
            await _botClient.SendMessage(
                chatId: chatId,
                text: "❌ Произошла ошибка. Пожалуйста, попробуйте позже или обратитесь к разработчику.",
                cancellationToken: cancellationToken);
        }
    }
}
