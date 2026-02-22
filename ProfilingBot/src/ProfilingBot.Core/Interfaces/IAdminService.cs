using ProfilingBot.Core.Models;

namespace ProfilingBot.Core.Interfaces
{
    public interface IAdminService
    {
        // === БАЗОВЫЕ ПРОВЕРКИ ===
        Task<bool> IsAdminAsync(long userId);
        Task<bool> IsOwnerAsync(long userId);
        Task<bool> CanManageAdminsAsync(long userId); // Только для владельца

        // === ОБНОВЛЕНИЕ ID ===
        Task<bool> TryUpdateAdminIdAsync(long userId, string userName);

        // === УПРАВЛЕНИЕ АДМИНАМИ (только для owner) ===
        Task<List<AdminUser>> GetAdminsAsync();
        Task<bool> AddAdminAsync(AdminUser admin, long addedByUserId);
        Task<bool> RemoveAdminAsync(long userIdToRemove, long removedByUserId);
        Task<bool> RemoveAdminByUsernameAsync(string userName, long removedByUserId);

        // === СТАТИСТИКА (доступно всем админам) ===
        Task<DailyStats> GetDailyStatsAsync(DateTime date);
        Task<WeeklyStats> GetWeeklyStatsAsync(DateTime startDate);

        // === ЭКСПОРТ (доступно всем админам) ===
        Task<byte[]> ExportToExcelAsync(DateTime? from = null, DateTime? to = null);
    }
}
