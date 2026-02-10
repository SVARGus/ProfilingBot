using ProfilingBot.Core.Models;

namespace ProfilingBot.Core.Interfaces
{
    public interface IExportService
    {
        Task<byte[]> ExportSessionsToExcelAsync(
            List<TestSession> sessions,
            List<PersonalityType> personalityTypes,
            DateTime from,
            DateTime to,
            CancellationToken cancellationToken = default);

        //Task<byte[]> ExportSessionsToCsvAsync(
        //    List<TestSession> sessions,
        //    List<PersonalityType> personalityTypes,
        //    DateTime from,
        //    DateTime to);

        //Task<string> GenerateStatsReportAsync(
        //    List<TestSession> sessions,
        //    DateTime from,
        //    DateTime to);
    }
}
