using System.Text.Json.Serialization;

namespace ProfilingBot.Core.Models
{
    public class CardGenerationConfig
    {
        [JsonConstructor]
        public CardGenerationConfig(
            int width = 900,
            int height = 1200,
            int blockMarginLeft = 60,
            int blockMarginTop = 50,
            int blockMaxWidth = 600,
            string cardsDirectory = "assets/cards",
            TextConfig userNameConfig = null,
            TextConfig shortNameConfig = null,
            TextConfig sloganConfig = null,
            LineConfig shortNameLineConfig = null)
        {
            Width = width;
            Height = height;
            BlockMarginLeft = blockMarginLeft;
            BlockMarginTop = blockMarginTop;
            BlockMaxWidth = blockMaxWidth;
            CardsDirectory = cardsDirectory ?? "assets/cards";

            // Устанавливаем значения по умолчанию если null
            UserNameConfig = userNameConfig ?? new TextConfig("Inter", FontStyle.Regular, 32, 2, "#FFFFFF", 0);

            ShortNameConfig = shortNameConfig ?? new TextConfig("Inter", FontStyle.SemiBold, 40, 10, "#DCE6F2", 80);

            SloganConfig = sloganConfig ?? new TextConfig("Inter", FontStyle.Regular, 32, 0, "#AAB6C4", 0);

            ShortNameLineConfig = shortNameLineConfig ?? new LineConfig( "#DCE6F2", 0.7f, 2f, 10);
        }

        // Размеры картинки
        public int Width { get; } = 900;
        public int Height { get; } = 1200;

        // Позиционирование
        public int BlockMarginLeft { get; } = 60;
        public int BlockMarginTop { get; } = 50;
        public int BlockMaxWidth { get; } = 600;

        // Путь к карточкам (относительный)
        public string CardsDirectory { get; } = "assets/cards";

        // Конфигурация текстовых блоков
        public TextConfig UserNameConfig { get; }
        public TextConfig ShortNameConfig { get; }
        public TextConfig SloganConfig { get; }

        // Конфигурация линии
        public LineConfig ShortNameLineConfig { get; }
    }

    public class TextConfig
    {
        [JsonConstructor]
        public TextConfig(
            string fontFamily = "Inter",
            FontStyle fontStyle = FontStyle.Regular,
            float fontSize = 32,
            float letterSpacingPercent = 0,
            string colorHex = "#FFFFFF",
            int yOffset = 0)
        {
            FontFamily = fontFamily ?? "Inter";
            FontStyle = fontStyle;
            FontSize = fontSize;
            LetterSpacingPercent = letterSpacingPercent;
            ColorHex = colorHex ?? "#FFFFFF";
            YOffset = yOffset;
        }

        public string FontFamily { get; }
        public FontStyle FontStyle { get; }
        public float FontSize { get; }
        public float LetterSpacingPercent { get; }
        public string ColorHex { get; }
        public int YOffset { get; }
    }

    public class LineConfig
    {
        [JsonConstructor]
        public LineConfig(
            string colorHex = "#DCE6F2",
            float opacity = 0.7f,
            float strokeWidth = 2f,
            int verticalOffset = 10)
        {
            ColorHex = colorHex ?? "#DCE6F2";
            Opacity = opacity;
            StrokeWidth = strokeWidth;
            VerticalOffset = verticalOffset;
        }

        public string ColorHex { get; }
        public float Opacity { get; }
        public float StrokeWidth { get; }
        public int VerticalOffset { get; }
    }

    public enum FontStyle
    {
        Regular,
        SemiBold,
        Bold,
        Italic
    }
}