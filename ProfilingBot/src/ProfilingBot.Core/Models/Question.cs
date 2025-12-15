using System.Text.Json.Serialization;

namespace ProfilingBot.Core.Models
{
    public class Question
    {
        [JsonConstructor]
        public Question(int id, string text, List<AnswerOption> answer)
        { 
            Id = id;
            Text = text;
            Answers = answer;
        }
        public int Id { get; private set; }
        public string Text { get; private set; } = string.Empty;
        public List<AnswerOption> Answers { get; private set; } = new();
    }
}