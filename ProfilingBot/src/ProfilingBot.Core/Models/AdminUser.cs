using System.Text.Json.Serialization;

namespace ProfilingBot.Core.Models
{
    public class AdminUser
    {
        public long UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Role { get; set; } = "admin"; // "owner" | "admin", Позже при расширение ролей можно будет добавить enum ролей
        public DateTime AddedAt { get; set; }
        public string AddedBy { get; set; } = string.Empty; // Кто добавил

        public bool IsOwner => Role == "owner";
        public bool CanManageAdmins => Role == "owner"; // Только владелец может управлять админами
    }
}