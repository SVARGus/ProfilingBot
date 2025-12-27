using System.Reflection;
using ProfilingBot.Core.Interfaces;
using ProfilingBot.Core.Models;
using SkiaSharp;

namespace ProfilingBot.Core.Services
{
    /// <summary>
    /// Генератор карточек с использованием SkiaSharp
    /// </summary>
    public class StoryCardGenerator : IStoryCardGenerator
    {
        private readonly CardGenerationConfig _cardGenerationConfig;
        private readonly IConfigurationService _configurationService;
        private readonly ILoggerService _loggerService;
        private readonly Assembly _assembly;

        // Кэширование ресурсов
        private SKTypeface? _cachedRegularFont;
        private SKTypeface? _cachedSemiBoldFont;
        private readonly Dictionary<int, SKImage> _cachedBackgrounds = new();

        public StoryCardGenerator(
            ILoggerService logger,
            IConfigurationService configurationService)
        {
            _cardGenerationConfig = new CardGenerationConfig();
            _loggerService = logger;
            _assembly = Assembly.GetExecutingAssembly();
            _configurationService = configurationService;
        }

        public async Task<byte[]> GenerateCardAsync(
            TestResult result,
            PersonalityType personalityType,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Загружаем актуальную конфигурацию
                var config = await _configurationService.GetCardGenerationConfigAsync();

                _loggerService.LogDebug($"Generating card for user {result.UserName}, type {personalityType.ShortName}");

                // 1. Загружаем фоновое изображение
                using var background = await LoadBackgroundAsync(personalityType.Id, config, cancellationToken);

                // 2. Создаем поверхность для рисования
                using var surface = SKSurface.Create(new SKImageInfo(
                    config.Width,
                    config.Height,
                    SKColorType.Rgba8888,
                    SKAlphaType.Premul));

                using var canvas = surface.Canvas;

                // 3. Рисуем фон
                canvas.DrawImage(background, 0, 0);

                // 4. Получаем шрифты
                var regularFont = await GetRegularFontAsync(cancellationToken);
                var semiBoldFont = await GetSemiBoldFontAsync(cancellationToken);

                // 5. Вычисляем позиции
                var currentY = config.BlockMarginTop;

                // 6. Рисуем строку "UserName - Slogan"
                await DrawUserNameAndSloganAsync(
                    canvas,
                    result.UserName,
                    personalityType.Slogan,
                    config,
                    regularFont,
                    currentY);

                // 7. Рисуем ShortName
                currentY += config.ShortNameConfig.YOffset;
                var shortNameWidth = DrawShortNameAsync(
                    canvas,
                    personalityType.ShortName,
                    config,
                    semiBoldFont,
                    currentY);

                // 8. Рисуем линию под ShortName
                DrawLineUnderText(
                    canvas,
                    config.BlockMarginLeft,
                    currentY,
                    shortNameWidth,
                    config.ShortNameLineConfig,
                    config.ShortNameConfig.FontSize);

                // 9. Конвертируем в JPEG
                using var image = surface.Snapshot();
                using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);

                _loggerService.LogDebug("Card generated successfully");

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
            if (_cachedBackgrounds.TryGetValue(personalityTypeId, out var cachedImage))
            {
                return cachedImage;
            }

            var cardsDir = _configurationService.GetAbsolutePath(config.CardsDirectory);
            var imagePath = Path.Combine(cardsDir, $"{personalityTypeId}.png");

            _loggerService.LogDebug($"Loading background from: {imagePath}");

            if (!File.Exists(imagePath))
            {
                throw new FileNotFoundException($"Background image not found: {imagePath}");
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

                // Кэшируем (не dispose!)
                _cachedBackgrounds[personalityTypeId] = image;

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
            if (_cachedRegularFont != null)
            {
                return _cachedRegularFont;
            }

            // Пробуем загрузить embedded шрифт
            var font = await LoadEmbeddedFontAsync("Inter-Regular.ttf", cancellationToken);

            // Fallback на системный
            _cachedRegularFont = font ?? SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);

            return _cachedRegularFont;
        }

        private async Task<SKTypeface> GetSemiBoldFontAsync(CancellationToken cancellationToken)
        {
            if (_cachedSemiBoldFont != null)
            {
                return _cachedSemiBoldFont;
            }

            // Пробуем загрузить embedded шрифт
            var font = await LoadEmbeddedFontAsync("Inter-SemiBold.ttf", cancellationToken);

            // Fallback на системный
            _cachedSemiBoldFont = font ?? SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.SemiBold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);

            return _cachedSemiBoldFont;
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

                return SKTypeface.FromStream(memoryStream);
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