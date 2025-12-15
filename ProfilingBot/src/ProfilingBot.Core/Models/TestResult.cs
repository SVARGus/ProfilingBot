namespace ProfilingBot.Core.Models
{
    public class TestResult
    {
        public TestResult(
        Guid sessionId,
        long userId,
        string userName,
        DateTime startedAt,
        DateTime completedAt,
        int personalityTypeId,
        int[] scores, // scores[0] - резерв, scores[1..5] - баллы по типам
        byte[]? shareableCard = null)
        {
            SessionId = sessionId;
            UserId = userId;
            UserName = userName;
            StartedAt = startedAt;
            CompletedAt = completedAt;
            PersonalityTypeId = personalityTypeId;
            Scores = scores;
            ShareableCard = shareableCard;

            if (shareableCard != null)
            {
                CardGeneratedAt = DateTime.UtcNow;
            }
        }
        // Данные сессии
        public Guid SessionId { get; }
        public long UserId { get; }
        public string UserName { get; }
        public DateTime StartedAt { get; }
        public DateTime CompletedAt { get; }

        // Ссылка на тип личности (берем данные из конфигурации)
        public int PersonalityTypeId { get; }

        // Результаты подсчета
        public int[] Scores { get; }

        // Генерация карточки (генерируется по требованию)
        public byte[]? ShareableCard { get; private set; }
        public DateTime? CardGeneratedAt { get; private set; }

        // Вспомогательные свойства
        public int TotalScore => Scores.Sum();
        public int MaxScore => Scores.Max();
        public bool HasShareableCard => ShareableCard != null;

        public void SetShareableCard(byte[] card)
        {
            ShareableCard = card;
            CardGeneratedAt = DateTime.UtcNow;
        }
    }
}