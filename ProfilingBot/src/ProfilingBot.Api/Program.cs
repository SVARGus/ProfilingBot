using ProfilingBot.Cloud;
using ProfilingBot.Core.Interfaces;
using ProfilingBot.Core.Services;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Конфигурация
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables(); //Для BOT_TOKEN, CONFIG_PATH и т.д.

// Логирование
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Конфигурация сервисов
var configPath = builder.Configuration["CONFIG_PATH"] ?? "./config";
var dataPath = builder.Configuration["DATA_PATH"] ?? "./data";
var assetsPath = builder.Configuration["ASSETS_PATH"] ?? "./assets";
var botToken = builder.Configuration["BOT_TOKEN"];

// ========== ДОБАВЛЕНО: Отладочный вывод ==========
Console.WriteLine($"=== DEBUG CONFIGURATION ===");
Console.WriteLine($"CONFIG_PATH: {configPath}");
Console.WriteLine($"DATA_PATH: {dataPath}");
Console.WriteLine($"ASSETS_PATH: {assetsPath}");
Console.WriteLine($"BOT_TOKEN present: {!string.IsNullOrEmpty(botToken)}");
if (!string.IsNullOrEmpty(botToken))
{
    Console.WriteLine($"BOT_TOKEN first 10 chars: {botToken.Substring(0, Math.Min(10, botToken.Length))}...");
    Console.WriteLine($"BOT_TOKEN length: {botToken.Length}");
}
Console.WriteLine($"===========================");
// =================================================

// Устанавливаем переменные окружения для Core слоя
Environment.SetEnvironmentVariable("CONFIG_PATH", configPath);
Environment.SetEnvironmentVariable("ASSETS_PATH", assetsPath);
Environment.SetEnvironmentVariable("DATA_PATH", dataPath);

if (string.IsNullOrEmpty(botToken))
{
    // В development можно использовать User Secrets
    if (builder.Environment.IsDevelopment())
    {
        throw new InvalidOperationException(
            "BOT_TOKEN is not configured. Please set it using:\n" +
            "1. User Secrets: dotnet user-secrets set \"BOT_TOKEN\" \"YOUR_TOKEN\"\n" +
            "2. Environment variable: export BOT_TOKEN=YOUR_TOKEN\n" +
            "3. appsettings.Development.json: Add \"BOT_TOKEN\": \"YOUR_TOKEN\""
        );
    }
    else
    {
        throw new InvalidOperationException("BOT_TOKEN is not configured");
    }
}

// ========== ДОБАВЛЕНО: Настройка зависимостей ==========
builder.Services.AddCoreServices(configPath, dataPath);
builder.Services.AddTelegramBot(botToken);
// =======================================================

// Контроллеры
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

// Health checks (базовый, для будущего мониторинга)
builder.Services.AddHealthChecks();

Console.WriteLine("=== VERIFYING PATHS ===");
Console.WriteLine($"Config path exists: {Directory.Exists(configPath)} -> {configPath}");
Console.WriteLine($"Assets path exists: {Directory.Exists(assetsPath)} -> {assetsPath}");
Console.WriteLine($"Data path exists: {Directory.Exists(dataPath)} -> {dataPath}");

// Проверяем карточки
var cardsPath = Path.Combine(assetsPath, "cards");
Console.WriteLine($"Cards path exists: {Directory.Exists(cardsPath)} -> {cardsPath}");
if (Directory.Exists(cardsPath))
{
    var cardFiles = Directory.GetFiles(cardsPath, "*.png");
    Console.WriteLine($"Found {cardFiles.Length} card images:");
    foreach (var file in cardFiles)
    {
        Console.WriteLine($"  - {Path.GetFileName(file)}");
    }
}
Console.WriteLine("========================");

var app = builder.Build();

// Конфигурация конвейера HTTP-запросов
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseRouting();
app.UseAuthorization();

// Важно: Отключаем глобальную обработку исключений для Telegram, 
// чтобы возвращать 200 OK даже при ошибках (требование Telegram API)
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception)
    {
        // Логируем, но возвращаем 200 OK
        context.Response.StatusCode = 200;
        await context.Response.WriteAsync("OK");
    }
});

app.MapHealthChecks("/health");
app.MapControllers(); // Все эндпоинты из контроллеров

app.Run();