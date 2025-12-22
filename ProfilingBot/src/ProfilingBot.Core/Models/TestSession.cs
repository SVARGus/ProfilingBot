namespace ProfilingBot.Core.Models
{
    public class TestSession
    {
        public Guid Id { get; set; } = Guid.NewGuid(); // Уникальный номер резултатов тестирования (втомчлисле незавершенного)
        public long UserId { get; set; } // Идентификатор пользователя
        public string UserName { get; set; } = string.Empty; // Имя пользователя, например "@SVARGuser"
        public DateTime StartedAt { get; set; } = DateTime.UtcNow; // Дата и время начала теста
        public DateTime? CompletedAt { get; set; } // Дата и время конца теста
        public int CurrentQuestionIndex { get; set; } // Идинтефикатор текущего вопроса
        public Dictionary<int, int> Answers { get; set; } = new(); // QuestionId -> AnswerId
        public int ResultIdPersonalityType { get; set; } // Id типа личности
        public string? ResultNamePersonalityType { get; set; } // Имя типа личности
        public bool IsCompleted => CompletedAt.HasValue; // Флаг завершенности теста

        // ========== ДОБАВЛЕНО: Рандомизация ==========
        /// <summary>
        /// Порядок вопросов в этой сессии (список оригинальных QuestionId)
        /// Например: [3, 1, 5, 2, 8, 4, 7, 6]
        /// </summary>
        public List<int> QuestionOrder { get; set; } = new();

        /// <summary>
        /// Порядок вариантов ответов для каждого вопроса
        /// Ключ: оригинальный QuestionId, Значение: список оригинальных AnswerId в случайном порядке
        /// Например: { 1: [2, 4, 1, 3, 5], 2: [5, 3, 1, 4, 2], ... }
        /// </summary>
        public Dictionary<int, List<int>> AnswerOrder { get; set; } = new();
        // =============================================

        /// <summary>
        /// Получить оригинальный QuestionId по индексу вопроса в сессии
        /// </summary>
        public int GetOriginalQuestionId(int sessionQuestionIndex)
        {
            if (sessionQuestionIndex >= 1 && sessionQuestionIndex <= QuestionOrder.Count)
            {
                return QuestionOrder[sessionQuestionIndex - 1];
            }
            throw new ArgumentOutOfRangeException(nameof(sessionQuestionIndex));
        }

        /// <summary>
        /// Получить оригинальный AnswerId по индексу варианта в сессии
        /// </summary>
        public int GetOriginalAnswerId(int questionId, int sessionAnswerIndex)
        {
            if (AnswerOrder.TryGetValue(questionId, out var order) &&
                sessionAnswerIndex >= 1 && sessionAnswerIndex <= order.Count)
            {
                return order[sessionAnswerIndex - 1];
            }
            throw new ArgumentOutOfRangeException(nameof(sessionAnswerIndex));
        }
    }
}