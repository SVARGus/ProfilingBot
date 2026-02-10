using ProfilingBot.Core.Interfaces;
using ProfilingBot.Core.Models;
using System.Reflection;
using System.Text.Json;

namespace ProfilingBot.Core.Services
{
    public class AdminService : IAdminService
    {
        private readonly IStorageService _storageService;
        private readonly IConfigurationService _configService;
        private readonly IExportService _exportService;
        private readonly ILoggerService _logger;

        private readonly string _adminsConfigPath;

        private List<AdminUser> _cachedAdmins = new();
        private DateTime _lastCacheUpdate = DateTime.MinValue;
        private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);

        public AdminService(
            IStorageService storageService,
            IConfigurationService configService,
            IExportService exportService,
            ILoggerService logger)
        {
            _storageService = storageService;
            _configService = configService;
            _exportService = exportService;
            _logger = logger;

            _adminsConfigPath = GetAdminsConfigPath();

            LoadAdminsAsync().Wait();

            _logger.LogInfo("AdminService initialized");
        }

        // === БАЗОВЫЕ ПРОВЕРКИ ===
        public async Task<bool> IsAdminAsync(long userId)
        {
            return await IsAdminAsync(userId, null);
        }

        public async Task<bool> IsOwnerAsync(long userId)
        {
            await EnsureCacheFreshAsync();

            // 1. Прямая проверка по ID
            var owner = _cachedAdmins.FirstOrDefault(a => a.UserId == userId && a.Role == "owner");
            if (owner != null)
            {
                _logger.LogDebug($"User {userId} found as owner by ID: {owner.UserName}");
                return true;
            }

            return false;
        }

        public async Task<bool> CanManageAdminsAsync(long userId)
        {
            return await IsOwnerAsync(userId);
        }

        // === УПРАВЛЕНИЕ АДМИНАМИ ===
        public async Task<List<AdminUser>> GetAdminsAsync()
        {
            await EnsureCacheFreshAsync();
            return _cachedAdmins.OrderBy(a => a.Role == "owner" ? 0 : 1)
                               .ThenBy(a => a.AddedAt)
                               .ToList();
        }

        public async Task<bool> AddAdminAsync(AdminUser admin, long addedByUserId)
        {
            if (!await CanManageAdminsAsync(addedByUserId))
            {
                _logger.LogWarning($"User {addedByUserId} attempted to add admin without permission");
                return false;
            }

            // Проверяем, не существует ли уже
            if (_cachedAdmins.Any(a => (a.UserId == admin.UserId && a.UserId != 0) || // Уже есть с таким ненулевым ID
                (a.UserName.Equals(admin.UserName, StringComparison.OrdinalIgnoreCase) && a.UserId == 0 && admin.UserId == 0) || // Оба с ID=0 и одинаковые usernames
                (a.UserName.Equals(admin.UserName, StringComparison.OrdinalIgnoreCase) && a.UserId != 0 && admin.UserId == 0))) // Существует с ненулевым ID, а мы добавляем с 0
            {
                _logger.LogWarning($"Admin already exists: {admin.UserName} (ID: {admin.UserId})");
                return false;
            }

            _cachedAdmins.Add(admin);
            await SaveAdminsAsync();

            _logger.LogInfo($"Admin added: {admin.UserName} (ID: {admin.UserId}) by {addedByUserId}");
            return true;
        }

        public async Task<bool> RemoveAdminAsync(long userIdToRemove, long removedByUserId)
        {
            if (!await CanManageAdminsAsync(removedByUserId))
            {
                _logger.LogWarning($"User {removedByUserId} attempted to remove admin without permission");
                return false;
            }

            var adminToRemove = _cachedAdmins.FirstOrDefault(a => a.UserId == userIdToRemove);
            if (adminToRemove == null)
            {
                _logger.LogWarning($"Admin not found for removal: {userIdToRemove}");
                return false;
            }

            if (adminToRemove.Role == "owner")
            {
                _logger.LogWarning($"Cannot remove owner: {userIdToRemove}");
                return false;
            }

            _cachedAdmins.Remove(adminToRemove);
            await SaveAdminsAsync();

            _logger.LogInfo($"Admin removed: {adminToRemove.UserName} (ID: {adminToRemove.UserId}) by {removedByUserId}");
            return true;
        }

        // === СТАТИСТИКА ===
        public async Task<DailyStats> GetDailyStatsAsync(DateTime date)
        {
            var startOfDay = date.Date;
            var endOfDay = startOfDay.AddDays(1).AddTicks(-1);

            var sessions = await _storageService.GetCompletedSessionsAsync(startOfDay, endOfDay);
            var personalityTypes = await _configService.GetPersonalityTypesAsync();

            var stats = new DailyStats
            {
                Date = date.Date,
                TotalTestsCompleted = sessions.Count,
                TotalUniqueUsers = sessions.Select(s => s.UserId).Distinct().Count(),
                PersonalityTypeDistribution = CalculateTypeDistribution(sessions, personalityTypes),
                AbandonedTests = await GetAbandonedTestsCountAsync(startOfDay, endOfDay)
            };

            if (sessions.Any())
            {
                stats.AverageTestDuration = CalculateAverageDuration(sessions);
                stats.MostPopularPersonalityType = GetMostPopularType(stats.PersonalityTypeDistribution);
                stats.TestsPerHour = CalculateTestsPerHour(sessions, startOfDay);
            }

            return stats;
        }

        public async Task<WeeklyStats> GetWeeklyStatsAsync(DateTime startDate)
        {
            var startOfWeek = startDate.Date;
            var endOfWeek = startOfWeek.AddDays(7).AddTicks(-1);

            var sessions = await _storageService.GetCompletedSessionsAsync(startOfWeek, endOfWeek);
            var personalityTypes = await _configService.GetPersonalityTypesAsync();

            var dailyCounts = new Dictionary<DateTime, int>();
            for (int i = 0; i < 7; i++)
            {
                var day = startOfWeek.AddDays(i);
                dailyCounts[day] = sessions.Count(s =>
                    s.CompletedAt.HasValue &&
                    s.CompletedAt.Value.Date == day);
            }

            var stats = new WeeklyStats
            {
                PeriodStart = startOfWeek,
                PeriodEnd = endOfWeek,
                TotalTestsCompleted = sessions.Count,
                TotalUniqueUsers = sessions.Select(s => s.UserId).Distinct().Count(),
                PersonalityTypeDistribution = CalculateTypeDistribution(sessions, personalityTypes),
                DailyCompletionCount = dailyCounts
            };

            if (sessions.Any())
            {
                stats.AverageTestDuration = CalculateAverageDuration(sessions);
                stats.MostPopularPersonalityType = GetMostPopularType(stats.PersonalityTypeDistribution);
            }

            return stats;
        }

        // === ЭКСПОРТ ===
        public async Task<byte[]> ExportToExcelAsync(DateTime? from = null, DateTime? to = null)
        {
            from ??= DateTime.MinValue;
            to ??= DateTime.UtcNow;

            // 1. Получаем данные
            var sessions = await _storageService.GetCompletedSessionsAsync(from, to);
            var personalityTypes = await _configService.GetPersonalityTypesAsync();

            // 2. Делегируем экспорт специализированному сервису
            return await _exportService.ExportSessionsToExcelAsync(
                sessions,
                personalityTypes,
                from.Value,
                to.Value);
        }

        // === ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ===
        private async Task EnsureCacheFreshAsync()
        {
            if (DateTime.UtcNow - _lastCacheUpdate > _cacheDuration)
            {
                await LoadAdminsAsync();
            }
        }

        private string GetAdminsConfigPath()
        {
            try
            {
                // Пробуем несколько вариантов:
                var possiblePaths = new List<string>();

                // 1. Рядом с другими конфигами (относительно configService)
                var basePath = _configService.GetBasePath();
                possiblePaths.Add(Path.Combine(basePath, "config", "admins.json"));

                // 2. В папке проекта (для разработки)
                possiblePaths.Add(Path.Combine(Directory.GetCurrentDirectory(), "config", "admins.json"));

                // 3. На 3 уровня выше (из bin/Debug/net8.0/)
                var assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (!string.IsNullOrEmpty(assemblyLocation))
                {
                    possiblePaths.Add(Path.Combine(assemblyLocation, "..", "..", "..", "config", "admins.json"));
                }

                // 4. В корневой папке проекта
                possiblePaths.Add("D:\\My_Project_IT\\Телеграмм-бот для TecForce\\profiling-bot\\config\\admins.json");

                foreach (var path in possiblePaths)
                {
                    var fullPath = Path.GetFullPath(path);
                    if (File.Exists(fullPath))
                    {
                        _logger.LogInfo($"Found admins config at: {fullPath}");
                        return fullPath;
                    }
                }

                // Если не нашли - создаем по первому пути
                var fallbackPath = Path.Combine(basePath, "config", "admins.json");
                _logger.LogWarning($"Admins config not found, will create at: {fallbackPath}");
                return fallbackPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error determining admins config path");
                return "config/admins.json"; // Fallback
            }
        }

        private async Task LoadAdminsAsync()
        {
            try
            {
                _logger.LogDebug($"Looking for admins config at: {_adminsConfigPath}");

                if (!File.Exists(_adminsConfigPath))
                {
                    _logger.LogWarning($"Admins config not found: {_adminsConfigPath}. Creating default.");
                    _cachedAdmins = CreateDefaultAdmins();

                    // Создаем директорию если её нет
                    var directory = Path.GetDirectoryName(_adminsConfigPath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    await SaveAdminsAsync();
                    return;
                }

                var json = await File.ReadAllTextAsync(_adminsConfigPath);
                var admins = JsonSerializer.Deserialize<List<AdminUser>>(json)
                    ?? new List<AdminUser>();

                // ДОБАВЛЯЕМ ДИАГНОСТИКУ
                _logger.LogInfo($"===== ADMIN LOADING DIAGNOSTICS =====");
                _logger.LogInfo($"Raw JSON length: {json.Length} chars");
                _logger.LogInfo($"Deserialized {admins.Count} admins:");

                foreach (var admin in admins)
                {
                    _logger.LogInfo($"  - ID: {admin.UserId}, Name: {admin.UserName}, Role: {admin.Role}");
                }

                // Проверяем, есть ли owner с ненулевым ID
                //var owners = admins.Where(a => a.Role == "owner" && a.UserId != 0).ToList();
                //if (!owners.Any())
                //{
                //    _logger.LogWarning($"No valid owner found (with non-zero ID)! Owners in config: {admins.Count(a => a.Role == "owner")}");
                //    _logger.LogWarning($"Adding you as owner: ID=1088014818, Name=@SVARGuser");

                //    // Добавляем вас как owner
                //    admins.Add(new AdminUser
                //    {
                //        UserId = 1088014818,
                //        UserName = "@SVARGuser",
                //        Role = "owner",
                //        AddedAt = DateTime.UtcNow,
                //        AddedBy = "system_fix"
                //    });
                //}
                //else
                //{
                //    _logger.LogInfo($"Found {owners.Count} valid owner(s). First owner ID: {owners.First().UserId}");
                //}

                _cachedAdmins = admins;
                _lastCacheUpdate = DateTime.UtcNow;

                _logger.LogInfo($"Total cached admins: {_cachedAdmins.Count}");
                _logger.LogInfo($"===== END DIAGNOSTICS =====");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to load admins from {_adminsConfigPath}");
                _cachedAdmins = CreateDefaultAdmins();
            }
        }

        private async Task SaveAdminsAsync()
        {
            try
            {
                var json = JsonSerializer.Serialize(_cachedAdmins, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                // Создаем директорию если её нет
                var directory = Path.GetDirectoryName(_adminsConfigPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                    _logger.LogDebug($"Created directory: {directory}");
                }

                await File.WriteAllTextAsync(_adminsConfigPath, json);
                _lastCacheUpdate = DateTime.UtcNow;

                _logger.LogDebug($"Saved {_cachedAdmins.Count} admins to: {_adminsConfigPath}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to save admins to {_adminsConfigPath}");
                throw;
            }
        }

        private List<AdminUser> CreateDefaultAdmins()
        {
            return new List<AdminUser>
            {
                new AdminUser
                {
                    UserId = 0, // Заполнится позже
                    UserName = "@SalandJulia",
                    Role = "owner",
                    AddedAt = DateTime.UtcNow,
                    AddedBy = "system"
                }
            };
        }

        private Dictionary<string, int> CalculateTypeDistribution(
            List<TestSession> sessions,
            List<PersonalityType> personalityTypes)
        {
            var distribution = new Dictionary<string, int>();

            foreach (var type in personalityTypes)
            {
                var count = sessions.Count(s => s.ResultNamePersonalityType == type.Name);
                distribution[type.Name] = count;
            }

            return distribution;
        }

        private async Task<int> GetAbandonedTestsCountAsync(DateTime from, DateTime to)
        {
            // Получаем активные сессии, начатые в период
            var activeSessions = await _storageService.GetAllActiveSessionsAsync();
            return activeSessions.Count(s =>
                s.StartedAt >= from &&
                s.StartedAt <= to &&
                !s.IsCompleted);
        }

        private TimeSpan CalculateAverageDuration(List<TestSession> sessions)
        {
            var completedSessions = sessions.Where(s => s.CompletedAt.HasValue).ToList();
            if (!completedSessions.Any()) return TimeSpan.Zero;

            var totalDuration = completedSessions
                .Sum(s => (s.CompletedAt!.Value - s.StartedAt).TotalSeconds);

            return TimeSpan.FromSeconds(totalDuration / completedSessions.Count);
        }

        private string GetMostPopularType(Dictionary<string, int> distribution)
        {
            if (!distribution.Any()) return "Нет данных";

            return distribution.OrderByDescending(kvp => kvp.Value)
                              .First().Key;
        }

        private int CalculateTestsPerHour(List<TestSession> sessions, DateTime date)
        {
            var sessionsToday = sessions.Where(s =>
                s.CompletedAt.HasValue &&
                s.CompletedAt.Value.Date == date.Date).ToList();

            if (!sessionsToday.Any()) return 0;

            var hours = sessionsToday.GroupBy(s => s.CompletedAt!.Value.Hour)
                                    .Select(g => new { Hour = g.Key, Count = g.Count() })
                                    .ToList();

            return (int)hours.Average(h => h.Count);
        }

        // === НОВЫЙ МЕТОД: Обновить ID админа при совпадении username ===
        public async Task<bool> TryUpdateAdminIdAsync(long userId, string userName)
        {
            await EnsureCacheFreshAsync();

            _logger.LogDebug($"Trying to update admin ID: username='{userName}' -> userId={userId}");

            // Ищем админа с совпадающим username и UserId = 0
            var adminToUpdate = _cachedAdmins.FirstOrDefault(a =>
                a.UserId == 0 &&
                !string.IsNullOrEmpty(a.UserName) &&
                a.UserName.Equals(userName, StringComparison.OrdinalIgnoreCase));

            if (adminToUpdate == null)
            {
                // Также проверяем без @
                var cleanUserName = userName.StartsWith("@") ? userName.Substring(1) : userName;
                adminToUpdate = _cachedAdmins.FirstOrDefault(a =>
                    a.UserId == 0 &&
                    !string.IsNullOrEmpty(a.UserName) &&
                    (a.UserName.Equals($"@{cleanUserName}", StringComparison.OrdinalIgnoreCase) ||
                     a.UserName.Equals(cleanUserName, StringComparison.OrdinalIgnoreCase)));
            }

            if (adminToUpdate != null)
            {
                _logger.LogInfo($"Updating admin ID: {adminToUpdate.UserName} from {adminToUpdate.UserId} to {userId}");

                adminToUpdate.UserId = userId;
                await SaveAdminsAsync();

                return true;
            }

            _logger.LogDebug($"No admin found with username '{userName}' and ID=0");
            return false;
        }

        // === ОБНОВЛЕННЫЙ IsAdminAsync с обновлением ID ===
        public async Task<bool> IsAdminAsync(long userId, string? userName)
        {
            await EnsureCacheFreshAsync();

            _logger.LogDebug($"Checking admin status: userId={userId}, userName={userName}");

            // 1. Прямая проверка по ID
            if (_cachedAdmins.Any(a => a.UserId == userId))
            {
                _logger.LogDebug($"User {userId} found as admin by ID");
                return true;
            }

            // 2. Если передан username, пробуем обновить ID
            if (!string.IsNullOrEmpty(userName) && await TryUpdateAdminIdAsync(userId, userName))
            {
                // После обновления проверяем снова
                if (_cachedAdmins.Any(a => a.UserId == userId))
                {
                    _logger.LogDebug($"User {userId} ({userName}) now recognized as admin after ID update");
                    return true;
                }
            }

            // 3. Проверка по username (для UserId = 0)
            if (!string.IsNullOrEmpty(userName))
            {
                var cleanUserName = userName.StartsWith("@") ? userName : $"@{userName}";
                var isAdminByUsername = _cachedAdmins.Any(a =>
                    a.UserId == 0 &&
                    !string.IsNullOrEmpty(a.UserName) &&
                    a.UserName.Equals(cleanUserName, StringComparison.OrdinalIgnoreCase));

                if (isAdminByUsername)
                {
                    _logger.LogDebug($"User {userId} ({userName}) found as admin by username (ID=0)");
                }

                return isAdminByUsername;
            }

            _logger.LogDebug($"User {userId} is NOT admin");
            return false;
        }
    }
}
