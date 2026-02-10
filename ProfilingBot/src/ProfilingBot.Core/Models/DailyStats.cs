namespace ProfilingBot.Core.Models
{
    public class DailyStats
    {
        public DateTime Date { get; set; } // Дата отчета 
        public int TotalTestsCompleted { get; set; } // Количество завершенных тестов
        public int TotalUniqueUsers { get; set; } // Количество пользователей которые впервые прошли тест
        public Dictionary<string, int> PersonalityTypeDistribution { get; set; } // Тип личности -> количество
        public TimeSpan AverageTestDuration { get; set; } // Среднее время теста:
        public string MostPopularPersonalityType { get; set; } // Самый популярный тип
        public int TestsPerHour { get; set; } // или массив по часам
        public int AbandonedTests { get; set; } // начали но не завершили
    }
}