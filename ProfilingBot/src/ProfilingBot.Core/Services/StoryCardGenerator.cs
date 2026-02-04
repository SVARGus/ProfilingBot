using Microsoft.Extensions.DependencyInjection;
using ProfilingBot.Core.Interfaces;
using ProfilingBot.Core.Models;
using SkiaSharp;
using System.Reflection;

namespace ProfilingBot.Core.Services
{
    /// <summary>
    /// Генератор карточек с использованием SkiaSharp
    /// </summary>
    public class StoryCardGenerator : IStoryCardGenerator
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILoggerService _loggerService;
        private readonly Assembly _assembly;

        // Ленивые зависимости
        private CardGenerationConfig? _cardGenerationConfig;
        private IConfigurationService? _configurationService;

        // Кэширование ресурсов
        private SKTypeface? _cachedRegularFont;
        private SKTypeface? _cachedSemiBoldFont;
        private readonly Dictionary<int, SKImage> _cachedBackgrounds = new();

        // Lock для потокобезопасности
        private readonly object _fontLock = new();
        private readonly object _backgroundLock = new();

        public StoryCardGenerator(
            ILoggerService logger,
            IServiceProvider serviceProvider)
        {
            _loggerService = logger;
            _assembly = Assembly.GetExecutingAssembly();
            _serviceProvider = serviceProvider;
        }

        private IConfigurationService ConfigService
        {
            get
            {
                if (_configurationService == null)
                {
                    using var scope = _serviceProvider.CreateScope();
                    _configurationService = scope.ServiceProvider.GetRequiredService<IConfigurationService>();
                    _loggerService.LogDebug("IConfigurationService initialized lazily");
                }
                return _configurationService;
            }
        }

        private async Task<CardGenerationConfig> GetConfigAsync()
        {
            if (_cardGenerationConfig == null)
            {
                _cardGenerationConfig = await ConfigService.GetCardGenerationConfigAsync();
                _loggerService.LogDebug("CardGenerationConfig loaded and cached");
            }
            return _cardGenerationConfig;
        }

        public async Task<byte[]> GenerateCardAsync(
            TestResult result,
            PersonalityType personalityType,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _loggerService.LogDebug($"Generating card for user {result.UserName}, type {personalityType.ShortName}");

                // 1. Получаем конфигурацию
                var config = await GetConfigAsync();

                // 2. Загружаем фоновое изображение
                using var background = await LoadBackgroundAsync(personalityType.Id, config, cancellationToken);

                // 3. Создаем поверхность для рисования
                using var surface = SKSurface.Create(new SKImageInfo(
                    config.Width,
                    config.Height,
                    SKColorType.Rgba8888,
                    SKAlphaType.Premul));

                using var canvas = surface.Canvas;

                // 4. Рисуем фон
                canvas.DrawImage(background, 0, 0);

                // 5. Получаем шрифты
                var regularFont = await GetRegularFontAsync(cancellationToken);
                var semiBoldFont = await GetSemiBoldFontAsync(cancellationToken);

                // 6. Вычисляем позиции
                var currentY = config.BlockMarginTop;

                // 7. Рисуем строку "UserName - Slogan"
                await DrawUserNameAndSloganAsync(
                    canvas,
                    result.UserName,
                    personalityType.Slogan,
                    config,
                    regularFont,
                    currentY);

                // 8. Рисуем ShortName
                currentY += config.ShortNameConfig.YOffset;
                var shortNameWidth = DrawShortNameAsync(
                    canvas,
                    personalityType.ShortName,
                    config,
                    semiBoldFont,
                    currentY);

                // 9. Рисуем линию под ShortName
                DrawLineUnderText(
                    canvas,
                    config.BlockMarginLeft,
                    currentY,
                    shortNameWidth,
                    config.ShortNameLineConfig,
                    config.ShortNameConfig.FontSize);

                // 10. Конвертируем в JPEG
                using var image = surface.Snapshot();
                using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);

                _loggerService.LogDebug($"Card generated successfully for session {result.SessionId}");

                return data.ToArray();
            }
            catch (Exception ex)
            {
                _loggerService.LogError(ex, $"Failed to generate card for user {result.UserId}");
                throw;
            }
        }

        /// <summary>
        /// Загружает фоновое изображение из embedded ресурсов
        /// </summary>
        private async Task<SKImage> LoadBackgroundAsync(
            int personalityTypeId, 
            CardGenerationConfig config, 
            CancellationToken cancellationToken)
        {
            // Проверяем кэш
            lock (_backgroundLock)
            {
                if (_cachedBackgrounds.TryGetValue(personalityTypeId, out var cachedImage))
                {
                    _loggerService.LogDebug($"Using cached background for type {personalityTypeId}");
                    return cachedImage;
                }
            }

            var cardsDir = await ConfigService.GetCardsDirectoryPathAsync();
            var imagePath = Path.Combine(cardsDir, $"{personalityTypeId}.png");

            _loggerService.LogDebug($"=== DEBUG: Loading background ===");
            _loggerService.LogDebug($"Cards directory: {cardsDir}");
            _loggerService.LogDebug($"Image path: {imagePath}");
            _loggerService.LogDebug($"File exists: {File.Exists(imagePath)}");
            _loggerService.LogDebug($"=== END DEBUG ===");

            if (!File.Exists(imagePath))
            {
                // Пробуем найти в других местах перед выбросом исключения
                var fallbackPaths = new[]
                {
                    Path.Combine(Directory.GetCurrentDirectory(), "assets", "cards", $"{personalityTypeId}.png"),
                    Path.Combine(AppContext.BaseDirectory, "assets", "cards", $"{personalityTypeId}.png"),
                    Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "", "assets", "cards", $"{personalityTypeId}.png")
                };

                foreach (var fallbackPath in fallbackPaths)
                {
                    if (File.Exists(fallbackPath))
                    {
                        imagePath = fallbackPath;
                        _loggerService.LogInfo($"Found fallback image: {imagePath}");
                        break;
                    }
                }

                if (!File.Exists(imagePath))
                {
                    throw new FileNotFoundException($"Background image not found for type {personalityTypeId}. Tried: {imagePath}");
                }
            }

            try
            {
                using var stream = File.OpenRead(imagePath);
                using var bitmap = SKBitmap.Decode(stream);

                if (bitmap == null)
                {
                    throw new InvalidOperationException($"Failed to decode PNG: {imagePath}");
                }

                var image = SKImage.FromBitmap(bitmap);

                lock (_backgroundLock)
                {
                    // Кэшируем
                    _cachedBackgrounds[personalityTypeId] = image;
                    _loggerService.LogDebug($"Background cached for type {personalityTypeId}");
                }

                return image;
            }
            catch (Exception ex)
            {
                _loggerService.LogError(ex, $"Failed to load background for type {personalityTypeId}");
                throw;
            }
        }

        private async Task<SKTypeface> GetRegularFontAsync(CancellationToken cancellationToken)
        {
            lock (_fontLock)
            {
                if (_cachedRegularFont != null)
                {
                    return _cachedRegularFont;
                }
            }

            // Пробуем загрузить embedded шрифт
            var font = await LoadEmbeddedFontAsync("Inter-Regular.ttf", cancellationToken);

            // Fallback на системный
            var fallbackFont = font ?? SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);

            lock (_fontLock)
            {
                _cachedRegularFont ??= fallbackFont;
                return _cachedRegularFont;
            }
        }

        private async Task<SKTypeface> GetSemiBoldFontAsync(CancellationToken cancellationToken)
        {
            lock (_fontLock)
            {
                if (_cachedSemiBoldFont != null)
                {
                    return _cachedSemiBoldFont;
                }
            }

            // Пробуем загрузить embedded шрифт
            var font = await LoadEmbeddedFontAsync("Inter-SemiBold.ttf", cancellationToken);

            // Fallback на системный
            var fallbackFont = font ?? SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.SemiBold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);

            lock (_fontLock)
            {
                _cachedSemiBoldFont ??= fallbackFont;
                return _cachedSemiBoldFont;
            }
        }

        private async Task<SKTypeface?> LoadEmbeddedFontAsync(string fontFileName, CancellationToken cancellationToken)
        {
            var resourceName = $"ProfilingBot.Core.Assets.Fonts.{fontFileName}";

            try
            {
                using var stream = _assembly.GetManifestResourceStream(resourceName);

                if (stream == null)
                {
                    _loggerService.LogWarning($"Embedded font not found: {resourceName}");
                    return null;
                }

                // Копируем в MemoryStream, т.к. SKTypeface требует доступ к данным после закрытия stream
                using var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream, cancellationToken);
                memoryStream.Position = 0;

                var typeface = SKTypeface.FromStream(memoryStream);
                _loggerService.LogDebug($"Embedded font loaded: {fontFileName}");

                return typeface;
            }
            catch (Exception ex)
            {
                _loggerService.LogError(ex, $"Failed to load embedded font: {fontFileName}");
                return null;
            }
        }

        private async Task DrawUserNameAndSloganAsync(
            SKCanvas canvas,
            string userName,
            string slogan,
            CardGenerationConfig config,
            SKTypeface regularFont,
            float currentY)
        {
            var userNameText = $"{userName} - ";
            var sloganText = slogan;

            // Создаем SKFont для userName
            var userNameFont = new SKFont(regularFont, config.UserNameConfig.FontSize);
            var sloganFont = new SKFont(regularFont, config.SloganConfig.FontSize);

            // Настраиваем letter spacing
            userNameFont.ScaleX = 1.0f + (config.UserNameConfig.LetterSpacingPercent / 100f);
            sloganFont.ScaleX = 1.0f + (config.SloganConfig.LetterSpacingPercent / 100f);

            // Измеряем текст
            var userNameWidth = userNameFont.MeasureText(userNameText);
            var sloganWidth = sloganFont.MeasureText(sloganText);

            // Проверяем, не выходит ли за пределы
            var totalWidth = userNameWidth + sloganWidth;
            if (totalWidth > config.BlockMaxWidth)
            {
                // Укорачиваем userName если нужно
                var maxUserNameWidth = config.BlockMaxWidth - sloganWidth - 10;
                userNameText = ShortenText(userNameText, userNameFont, maxUserNameWidth);
                userNameWidth = userNameFont.MeasureText(userNameText);
            }

            // Создаем SKPaint для цветов
            using var userNamePaint = new SKPaint
            {
                Color = SKColor.Parse(config.UserNameConfig.ColorHex),
                IsAntialias = true
            };

            using var sloganPaint = new SKPaint
            {
                Color = SKColor.Parse(config.SloganConfig.ColorHex),
                IsAntialias = true
            };

            // Рисуем UserName
            canvas.DrawText(
                text: userNameText,
                x: config.BlockMarginLeft,
                y: currentY + config.UserNameConfig.FontSize,
                textAlign: SKTextAlign.Left,
                font: userNameFont,
                paint: userNamePaint);

            // Рисуем Slogan сразу после UserName
            canvas.DrawText(
                text: sloganText,
                x: config.BlockMarginLeft + userNameWidth,
                y: currentY + config.SloganConfig.FontSize,
                textAlign: SKTextAlign.Left,
                font: sloganFont,
                paint: sloganPaint);
        }

        private float DrawShortNameAsync(
            SKCanvas canvas,
            string shortName,
            CardGenerationConfig config,
            SKTypeface semiBoldFont,
            float currentY)
        {
            // Создаем SKFont
            var font = new SKFont(semiBoldFont, config.ShortNameConfig.FontSize);
            font.ScaleX = 1.0f + (config.ShortNameConfig.LetterSpacingPercent / 100f);

            // Создаем SKPaint
            using var paint = new SKPaint
            {
                Color = SKColor.Parse(config.ShortNameConfig.ColorHex),
                IsAntialias = true
            };

            // Рисуем текст
            canvas.DrawText(
                text: shortName,
                x: config.BlockMarginLeft,
                y: currentY + config.ShortNameConfig.FontSize,
                textAlign: SKTextAlign.Left,
                font: font,
                paint: paint);

            // Возвращаем ширину текста
            return font.MeasureText(shortName);
        }

        /// <summary>
        /// Укорачивает текст если он слишком длинный
        /// </summary>
        private string ShortenText(string text, SKFont font, float maxWidth)
        {
            if (font.MeasureText(text) <= maxWidth)
            {
                return text;
            }

            var shortened = text;
            while (shortened.Length > 3 && font.MeasureText(shortened + "...") > maxWidth)
            {
                shortened = shortened.Substring(0, shortened.Length - 1);
            }

            return shortened + "...";
        }

        /// <summary>
        /// Рисует линию под текстом
        /// </summary>
        private void DrawLineUnderText(
            SKCanvas canvas,
            float startX,
            float textY,
            float textWidth,
            LineConfig lineConfig,
            float fontSize)
        {
            using var paint = new SKPaint
            {
                Color = SKColor.Parse(lineConfig.ColorHex).WithAlpha((byte)(lineConfig.Opacity * 255)),
                StrokeWidth = lineConfig.StrokeWidth,
                IsStroke = true,
                IsAntialias = true
            };

            // Линия под текстом с отступом
            var lineY = textY + fontSize + lineConfig.VerticalOffset;
            canvas.DrawLine(startX, lineY, startX + textWidth, lineY, paint);
        }
    }
}