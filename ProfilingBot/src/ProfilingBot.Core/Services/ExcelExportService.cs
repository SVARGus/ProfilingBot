using ProfilingBot.Core.Helpers;
using ProfilingBot.Core.Interfaces;
using ProfilingBot.Core.Models;
using ClosedXML.Excel;

namespace ProfilingBot.Core.Services
{
    public class ExcelExportService : IExportService
    {
        private readonly ILoggerService _logger;

        public ExcelExportService(ILoggerService logger)
        {
            _logger = logger;
            _logger.LogInfo("ExcelExportService initialized");
        }

        public async Task<byte[]> ExportSessionsToExcelAsync(
            List<TestSession> sessions,
            List<PersonalityType> personalityTypes,
            DateTime from,
            DateTime to,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("Тесты");

                // Заголовки — основные
                worksheet.Cell(1, 1).Value = "ID сессии";
                worksheet.Cell(1, 2).Value = "ID пользователя";
                worksheet.Cell(1, 3).Value = "Имя пользователя";
                worksheet.Cell(1, 4).Value = "Дата начала";
                worksheet.Cell(1, 5).Value = "Дата завершения";
                worksheet.Cell(1, 6).Value = "Тип личности";
                worksheet.Cell(1, 7).Value = "Длительность (мин)";

                // Заголовки — ответы по вопросам (колонки 8-15)
                int totalQuestions = 8;
                for (int q = 1; q <= totalQuestions; q++)
                {
                    worksheet.Cell(1, 7 + q).Value = $"Вопрос {q}";
                }

                // Стиль заголовков
                var headerRange = worksheet.Range(1, 1, 1, 7 + totalQuestions);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

                // Данные
                int row = 2;
                foreach (var session in sessions.OrderByDescending(s => s.CompletedAt))
                {
                    worksheet.Cell(row, 1).Value = session.Id.ToString();
                    worksheet.Cell(row, 2).Value = session.UserId;
                    worksheet.Cell(row, 3).Value = session.UserName;
                    worksheet.Cell(row, 4).Value = TimeHelper.ToMoscowTime(session.StartedAt);
                    worksheet.Cell(row, 5).Value = session.CompletedAt.HasValue
                        ? TimeHelper.ToMoscowTime(session.CompletedAt.Value)
                        : (DateTime?)null;
                    worksheet.Cell(row, 6).Value = session.ResultNamePersonalityType ?? "Не определен";

                    if (session.CompletedAt.HasValue)
                    {
                        var duration = (session.CompletedAt.Value - session.StartedAt).TotalMinutes;
                        worksheet.Cell(row, 7).Value = Math.Round(duration, 2);
                    }

                    // Ответы по вопросам: session.Answers[questionId] → answerId
                    // Ключ — оригинальный QuestionId, поэтому данные попадают
                    // в правильный столбец независимо от порядка показа
                    for (int q = 1; q <= totalQuestions; q++)
                    {
                        if (session.Answers.TryGetValue(q, out var answerId))
                        {
                            worksheet.Cell(row, 7 + q).Value = answerId;
                        }
                    }

                    row++;
                }

                // Авто-ширина колонок
                worksheet.Columns().AdjustToContents();

                // Лист со статистикой по необходимости
                var statsSheet = workbook.Worksheets.Add("Статистика");
                AddStatistics(statsSheet, sessions, personalityTypes, from, to);

                // Сохраняем в MemoryStream
                using var stream = new MemoryStream();
                workbook.SaveAs(stream);

                _logger.LogInfo($"Exported {sessions.Count} sessions to Excel");
                return stream.ToArray();
            }, cancellationToken);
        }

        private void AddStatistics(
            IXLWorksheet worksheet,
            List<TestSession> sessions,
            List<PersonalityType> personalityTypes,
            DateTime from,
            DateTime to)
        {
            // Реализация статистики... (будет реализована позже по требованию)
        }
    }
}