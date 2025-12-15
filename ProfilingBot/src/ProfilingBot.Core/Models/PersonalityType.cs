using System.Text.Json.Serialization;

namespace ProfilingBot.Core.Models
{
    public class PersonalityType
    {
        [JsonConstructor]
        public PersonalityType(
            int id,
            string name,
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
        public string FullName { get; private set; } = string.Empty; // "Аналитический тип — Человек-логика"
        public string Description { get; private set; } = string.Empty; // Общее описание типа
        public string Strengths { get; private set; } = string.Empty; // "Ваша сила — структурность мышления и внимание к деталям."
        public string Sphere { get; private set; } = string.Empty; // "Ваша стихия: аналитика, планирование, консалтинг..."
        public string Recommendations { get; private set; } = string.Empty; // Дополнительное описание и рекомендации
        public string ImagePath { get; private set; } = string.Empty; // Адресс размещения картинки типа
        public string ShareTemplate { get; private set; } = string.Empty; // Возможно излишне???
    }
}