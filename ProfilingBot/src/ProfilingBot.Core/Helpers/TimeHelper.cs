namespace ProfilingBot.Core.Helpers
{
    public static class TimeHelper
    {
        private static readonly TimeZoneInfo MoscowTz = GetMoscowTimeZone();

        public static DateTime ToMoscowTime(DateTime utcTime)
        {
            return TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(utcTime, DateTimeKind.Utc), MoscowTz);
        }

        public static DateTime NowMoscow => ToMoscowTime(DateTime.UtcNow);

        private static TimeZoneInfo GetMoscowTimeZone()
        {
            // Windows: "Russian Standard Time", Linux/Docker: "Europe/Moscow"
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Russian Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Europe/Moscow");
            }
        }
    }
}
