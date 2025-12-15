using System.Text.Json.Serialization;

namespace ProfilingBot.Core.Models
{
    public class BotConfig
    {
        [JsonConstructor]
        public BotConfig(
            string name, 
            string welcomeMessage, 
            string channelLink, 
            string introMessage, 
            string completionMessage, 
            int totalQuestions, 
            int answersPerQuestion)
        {
            Name = name;
            WelcomeMessage = welcomeMessage;
            ChannelLink = channelLink;
            IntroMessage = introMessage;
            CompletionMessage = completionMessage;
            TotalQuestions = totalQuestions;
            AnswersPerQuestion = answersPerQuestion;
        }

        public string Name { get; private set; } = string.Empty;
        public string WelcomeMessage { get; private set; } = string.Empty;
        public string ChannelLink { get; private set; } = string.Empty;
        public string IntroMessage { get; private set; } = string.Empty;
        public string CompletionMessage { get; private set; } = string.Empty;
        public int TotalQuestions { get; private set; }
        public int AnswersPerQuestion { get; private set; }
    }
}