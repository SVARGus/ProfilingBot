using System.Security.Cryptography.X509Certificates;
using System.Text.Json.Serialization;

namespace ProfilingBot.Core.Models
{
    public class PersonalityType
    {
        [JsonConstructor]
        public PersonalityType(
            int id,
            string name,
            string shortName,         // краткое название для карточки
            string slogan,            // слоган для карточки
            string fullName,
            string description,
            string strengths,
            string sphere,
            string recommendations,
            string imagePath,
            string shareTemplate)
        {
            Id = id;
            Name = name;
            ShortName = shortName;
            Slogan = slogan;
            FullName = fullName;
            Description = description;
            Strengths = strengths;
            Sphere = sphere;
            Recommendations = recommendations;
            ImagePath = imagePath;
            ShareTemplate = shareTemplate;
        }

        public int Id { get; private set; }
        public string Name { get; private set; } = string.Empty; // Короткое имя (например, "Аналитик")
        public string ShortName { get; private set; } = string.Empty; // Краткое название типа для карточки(заглавными буквами в квадратных скобках), например [ АНАЛИТИК ]
        public string Slogan { get; private set; } = string.Empty; // Короткий слоган, описывающий тип личности
        public string FullName { get; private set; } = string.Empty; // "Аналитический тип — Человек-логика"
        public string Description { get; private set; } = string.Empty; // Общее описание типа
        public string Strengths { get; private set; } = string.Empty; // "Ваша сила — структурность мышления и внимание к деталям."
        public string Sphere { get; private set; } = string.Empty; // "Ваша стихия: аналитика, планирование, консалтинг..."
        public string Recommendations { get; private set; } = string.Empty; // Дополнительное описание и рекомендации
        public string ImagePath { get; private set; } = string.Empty; // Адресс размещения картинки типа
        public string ShareTemplate { get; private set; } = string.Empty; // Возможно излишне???
    }
}