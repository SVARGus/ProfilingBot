using System;
using System.Collections.Generic;
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
    }
}