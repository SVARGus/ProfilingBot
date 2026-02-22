using ProfilingBot.Core.Interfaces;
using ProfilingBot.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ProfilingBot.Core.Services
{
    public class FileStorageService :IStorageService
    {
        private readonly string _dataPath;
        private readonly ILoggerService _logger;
        private readonly object _activeSessionsLock = new();
        private readonly object _completedSessionsLock = new();

        public FileStorageService(string dataPath, ILoggerService logger)
        {
            _dataPath = dataPath;
            _logger = logger;

            // Создаем структуру папок
            Directory.CreateDirectory(Path.Combine(_dataPath, "active"));
            Directory.CreateDirectory(Path.Combine(_dataPath, "completed"));
            Directory.CreateDirectory(Path.Combine(_dataPath, "exports"));

            _logger.LogInfo($"Storage initialized at: {_dataPath}");
        }

        // === Пути к файлам ===

        private string ActiveSessionsFilePath => Path.Combine(_dataPath, "active", "active-sessions.json");
        private string CompletedSessionsFilePath => Path.Combine(_dataPath, "completed", "completed-sessions.json");

        public string GetCompletedSessionsFilePath() => CompletedSessionsFilePath;

        // === Активные сессии ===

        public async Task<TestSession?> GetActiveSessionAsync(Guid sessionId)
        {
            var sessions = await LoadActiveSessionsAsync();
            return sessions.FirstOrDefault(s => s.Id == sessionId);
        }

        public async Task<TestSession?> GetActiveSessionByUserIdAsync(long userId)
        {
            var sessions = await LoadActiveSessionsAsync();
            return sessions.FirstOrDefault(s => s.UserId == userId && !s.IsCompleted);
        }

        public async Task SaveActiveSessionAsync(TestSession session)
        {
            lock (_activeSessionsLock)
            {
                var sessions = LoadActiveSessions();

                // Удаляем старую версию сессии если существует
                var existingIndex = sessions.FindIndex(s => s.Id == session.Id);
                if (existingIndex >= 0)
                {
                    sessions[existingIndex] = session;
                }
                else
                {
                    sessions.Add(session);
                }

                SaveActiveSessions(sessions);

                _logger.LogDebug($"Saved active session {session.Id} for user {session.UserId}");
            }

            await Task.CompletedTask;
        }

        public async Task RemoveActiveSessionAsync(Guid sessionId)
        {
            lock (_activeSessionsLock)
            {
                var sessions = LoadActiveSessions();
                var sessionToRemove = sessions.FirstOrDefault(s => s.Id == sessionId);

                if (sessionToRemove != null)
                {
                    sessions.Remove(sessionToRemove);
                    SaveActiveSessions(sessions);

                    _logger.LogDebug($"Removed active session {sessionId}");
                }
            }

            await Task.CompletedTask;
        }

        public async Task<List<TestSession>> GetAllActiveSessionsAsync()
        {
            return await LoadActiveSessionsAsync();
        }

        // === Завершенные сессии ===

        public async Task SaveCompletedSessionAsync(TestSession session)
        {
            if (!session.IsCompleted || !session.CompletedAt.HasValue)
            {
                throw new InvalidOperationException("Session is not completed");
            }

            lock (_completedSessionsLock)
            {
                var sessions = LoadCompletedSessions();

                // Проверяем, не существует ли уже эта сессия
                var existingIndex = sessions.FindIndex(s => s.Id == session.Id);
                if (existingIndex >= 0)
                {
                    sessions[existingIndex] = session;
                }
                else
                {
                    sessions.Add(session);
                }

                SaveCompletedSessions(sessions);

                _logger.LogDebug($"Saved completed session {session.Id} for user {session.UserId}");
            }

            await Task.CompletedTask;
        }

        public async Task<List<TestSession>> GetCompletedSessionsAsync(DateTime? from = null, DateTime? to = null)
        {
            var sessions = await LoadCompletedSessionsAsync();

            return sessions
                .Where(s => s.CompletedAt.HasValue)
                .Where(s => from == null || s.CompletedAt >= from)
                .Where(s => to == null || s.CompletedAt <= to)
                .OrderByDescending(s => s.CompletedAt)
                .ToList();
        }

        public async Task<int> GetCompletedSessionsCountAsync()
        {
            var sessions = await LoadCompletedSessionsAsync();
            return sessions.Count;
        }

        // === Вспомогательные методы для работы с файлами ===

        private List<TestSession> LoadActiveSessions()
        {
            var filePath = ActiveSessionsFilePath;

            if (!File.Exists(filePath))
            {
                return new List<TestSession>();
            }

            try
            {
                var json = File.ReadAllText(filePath);
                var sessions = JsonSerializer.Deserialize<List<TestSession>>(json)
                    ?? new List<TestSession>();

                // Очищаем устаревшие сессии (старше 24 часов)
                var cutoffTime = DateTime.UtcNow.AddHours(-24);
                var freshSessions = sessions
                    .Where(s => s.StartedAt >= cutoffTime)
                    .ToList();

                if (freshSessions.Count != sessions.Count)
                {
                    SaveActiveSessions(freshSessions);
                    _logger.LogInfo($"Cleaned up {sessions.Count - freshSessions.Count} stale active sessions");
                }

                return freshSessions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to load active sessions from {filePath}");
                return new List<TestSession>();
            }
        }

        private async Task<List<TestSession>> LoadActiveSessionsAsync()
        {
            return await Task.Run(() => LoadActiveSessions());
        }

        private void SaveActiveSessions(List<TestSession> sessions)
        {
            var filePath = ActiveSessionsFilePath;

            try
            {
                var json = JsonSerializer.Serialize(sessions, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to save active sessions to {filePath}");
                throw;
            }
        }

        private List<TestSession> LoadCompletedSessions()
        {
            var filePath = CompletedSessionsFilePath;

            if (!File.Exists(filePath))
            {
                return new List<TestSession>();
            }

            try
            {
                var json = File.ReadAllText(filePath);
                return JsonSerializer.Deserialize<List<TestSession>>(json)
                    ?? new List<TestSession>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to load completed sessions from {filePath}");
                return new List<TestSession>();
            }
        }

        private async Task<List<TestSession>> LoadCompletedSessionsAsync()
        {
            return await Task.Run(() => LoadCompletedSessions());
        }

        private void SaveCompletedSessions(List<TestSession> sessions)
        {
            var filePath = CompletedSessionsFilePath;

            try
            {
                var json = JsonSerializer.Serialize(sessions, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to save completed sessions to {filePath}");
                throw;
            }
        }

        // Метод для инициализации и очистки данных при запуске
        public async Task InitializeAsync()
        {
            await Task.Run(() =>
            {
                // Создаем пустые файлы если их нет
                if (!File.Exists(ActiveSessionsFilePath))
                {
                    SaveActiveSessions(new List<TestSession>());
                }

                if (!File.Exists(CompletedSessionsFilePath))
                {
                    SaveCompletedSessions(new List<TestSession>());
                }

                _logger.LogInfo("Storage initialized with empty collections");
            });
        }
    }
}