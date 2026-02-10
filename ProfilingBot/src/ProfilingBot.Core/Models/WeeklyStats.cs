namespace ProfilingBot.Core.Models
{
    public class WeeklyStats
    {
        public DateTime PeriodStart { get; set; } // Начало периода отчета
        public DateTime PeriodEnd { get; set; } // Конец периода отчета
        public int TotalTestsCompleted { get; set; } // Количество завершенных тестов
        public int TotalUniqueUsers { get; set; } // Количество пользователей которые впервые прошли тест
        public Dictionary<string, int> PersonalityTypeDistribution { get; set; } // Распределение типов по количеству
        public Dictionary<DateTime, int> DailyCompletionCount { get; set; } // График по дням
        public TimeSpan AverageTestDuration { get; set; } // Среднее время теста:
        public string MostPopularPersonalityType { get; set; } // Самый популярный тип
    }
}