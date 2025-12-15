using System.Text.Json.Serialization;

namespace ProfilingBot.Core.Models
{
    public class AnswerOption
    {
        [JsonConstructor]
        public AnswerOption(int id, string text, int idPersonalityType)
        {
            Id = id;
            Text = text;
            IdPersonalityType = idPersonalityType;
        }
        public int Id { get; private set; }
        public string Text { get; private set; } = string.Empty;
        public int IdPersonalityType { get; private set; } // Связь с Типом личности по его Id
    }
}