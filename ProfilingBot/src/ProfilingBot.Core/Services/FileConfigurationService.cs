using ProfilingBot.Core.Interfaces;
using ProfilingBot.Core.Models;
using System.Reflection;
using System.Text.Json;

namespace ProfilingBot.Core.Services
{
    public class FileConfigurationService : IConfigurationService
    {
        private readonly string _configPath;
        private readonly ILoggerService _logger;

        private BotConfig? _cachedBotConfig;
        private List<Question>? _cachedQuestions;
        private List<PersonalityType>? _cachedPersonalityTypes;
        private CardGenerationConfig? _cachedCardGenerationConfig;

        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        public FileConfigurationService(string configPath, ILoggerService logger)
        {
            _configPath = configPath;
            _logger = logger;

            EnsureConfigDirectoryExists();

            //if (!Directory.Exists(_configPath))
            //{
            //    _logger.LogWarning($"Config directory not found: {_configPath}");
            //    _logger.LogInfo($"Current directory: {Directory.GetCurrentDirectory()}");

            //    // Пробуем найти config относительно текущей директории
            //    var alternativePaths = new[]
            //    {
            //        Path.Combine(Directory.GetCurrentDirectory(), "config"),
            //        Path.Combine(AppContext.BaseDirectory, "config"),
            //        Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "", "config")
            //    };

            //    foreach (var altPath in alternativePaths)
            //    {
            //        if (Directory.Exists(altPath))
            //        {
            //            _configPath = altPath;
            //            _logger.LogInfo($"Using alternative config path: {_configPath}");
            //            break;
            //        }
            //    }

            //    // Если все еще не найдено, создаем
            //    if (!Directory.Exists(_configPath))
            //    {
            //        Directory.CreateDirectory(_configPath);
            //        _logger.LogInfo($"Created config directory: {_configPath}");
            //    }
            //}
        }

        private void EnsureConfigDirectoryExists()
        {
            try
            {
                if (!Directory.Exists(_configPath))
                {
                    Directory.CreateDirectory(_configPath);
                    _logger.LogInfo($"Created config directory: {_configPath}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to create config directory: {_configPath}");
            }
        }

        public async Task<BotConfig> GetBotConfigAsync()
        {
            if (_cachedBotConfig != null)
            {
                return _cachedBotConfig;
            }

            var configPath = Path.Combine(_configPath, "test-config.json");

            if (!File.Exists(configPath))
            {
                _logger.LogWarning($"Config file not found: {configPath}. Usiong defaults.");
                _cachedBotConfig = GetDefaultBotConfig();
                return _cachedBotConfig;
            }

            try
            {
                var json = await File.ReadAllTextAsync(configPath);

                // Прямая десериализация - нет обертки!
                var botConfig = JsonSerializer.Deserialize<BotConfig>(json, _jsonOptions);

                if (botConfig == null)
                {
                    _logger.LogWarning($"Failed to deserialize bot config from {configPath}");
                    _cachedBotConfig = GetDefaultBotConfig();
                }
                else
                {
                    _cachedBotConfig = botConfig;
                    _logger.LogDebug($"Loaded bot config from: {configPath}");
                }

                return _cachedBotConfig;
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, $"Invalid JSON in bot config file: {configPath}");
                _cachedBotConfig = GetDefaultBotConfig();
                return _cachedBotConfig;
            }
            catch ( Exception ex ) 
            {
                _logger.LogError(ex, $"Failed to load bot config from {configPath}");
                _cachedBotConfig = GetDefaultBotConfig();
                return _cachedBotConfig;
            }
        }

        public async Task<List<Question>> GetQuestionsAsync()
        {
            if (_cachedQuestions != null)
            {
                return _cachedQuestions;
            }

            var questionsPath = Path.Combine(_configPath, "questions.json");

            if (!File.Exists(questionsPath))
            {
                _logger.LogWarning($"Questions file not found: {questionsPath}");
                _cachedQuestions = new List<Question>();
                return _cachedQuestions;
            }

            try
            {
                var json = await File.ReadAllTextAsync(questionsPath);

                var questions = JsonSerializer.Deserialize<List<Question>>(json, _jsonOptions);
                _cachedQuestions = questions ?? new List<Question>();

                ValidateQuestions(_cachedQuestions);
                _logger.LogDebug($"Loaded {_cachedQuestions.Count} questions from: {questionsPath}");

                return _cachedQuestions; ;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to load questions from {questionsPath}");
                _cachedQuestions = new List<Question>();
                return _cachedQuestions;
            }
        }

        public async Task<List<PersonalityType>> GetPersonalityTypesAsync()
        {
            if (_cachedPersonalityTypes != null)
            {
                return _cachedPersonalityTypes;
            }

            var typesPath = Path.Combine(_configPath, "personality-types.json");

            if (!File.Exists(typesPath))
            {
                _logger.LogWarning($"Personality types file not found: {typesPath}");
                _cachedPersonalityTypes = new List<PersonalityType>();
                return _cachedPersonalityTypes;
            }

            try
            {
                var json = await File.ReadAllTextAsync(typesPath);

                var personalityTypes = JsonSerializer.Deserialize<List<PersonalityType>>(json, _jsonOptions);
                _cachedPersonalityTypes = personalityTypes ?? new List<PersonalityType>();

                // Сортируем по ID для приоритета
                _cachedPersonalityTypes = _cachedPersonalityTypes
                    .OrderBy(t => t.Id)
                    .ToList();

                _logger.LogDebug($"Loaded {_cachedPersonalityTypes.Count} personality types from: {typesPath}");

                return _cachedPersonalityTypes;
            }
            catch ( Exception ex )
            {
                _logger.LogError(ex, $"Failed to load personality types from {typesPath}");
                _cachedPersonalityTypes = new List<PersonalityType>();
                return _cachedPersonalityTypes;
            }
        }

        public async Task<string> GetWelcomeMessageAsync()
        {
            var config = await GetBotConfigAsync();
            return config.WelcomeMessage;
        }

        public async Task<string> GetIntroMessageAsync()
        {
            var config = await GetBotConfigAsync();
            return config.IntroMessage;
        }

        public async Task<string> GetCompletionMessageAsync()
        {
            var config = await GetBotConfigAsync();
            return config.CompletionMessage;
        }

        public async Task<Question?> GetQuestionAsync(int questionId)
        {
            var questions = await GetQuestionsAsync();
            return questions.FirstOrDefault(q => q.Id == questionId);
        }

        public async Task<PersonalityType?> GetPersonalityTypeAsync(int personalityTypeId)
        {
            var types = await GetPersonalityTypesAsync();
            return types.FirstOrDefault(t => t.Id == personalityTypeId);
        }

        public async Task<bool> ValidateConfigurationAsync()
        {
            try
            {
                var botConfig = await GetBotConfigAsync();
                var questions = await GetQuestionsAsync();
                var personalityTypes = await GetPersonalityTypesAsync();

                // Проверяем базовые требования
                if (string.IsNullOrEmpty(botConfig.Name))
                {
                    _logger.LogError("Bot name is not configured");
                    return false;
                }

                if (questions.Count != botConfig.TotalQuestions)
                {
                    _logger.LogWarning($"Questions count mismatch: expected {botConfig.TotalQuestions}, got {questions.Count}");
                }

                // Проверяем, что у всех вопросов правильное количество ответов
                foreach (var question in questions)
                {
                    if (question.Answers.Count != botConfig.AnswersPerQuestion)
                    {
                        _logger.LogWarning($"Question {question.Id} has {question.Answers.Count} answers, expected {botConfig.AnswersPerQuestion}");
                    }

                    // Проверяем, что все IdPersonalityType существуют
                    foreach (var answer in question.Answers)
                    {
                        if (!personalityTypes.Any(t => t.Id == answer.IdPersonalityType))
                        {
                            _logger.LogError($"Question {question.Id}, Answer {answer.Id}: Invalid PersonalityType ID {answer.IdPersonalityType}");
                            return false;
                        }
                    }
                }

                // Проверяем, что есть хотя бы один тип личности
                if (personalityTypes.Count == 0)
                {
                    _logger.LogError("No personality types configured");
                    return false;
                }

                _logger.LogInfo("Configuration validation passed");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Configuration validation failed");
                return false;
            }
        }

        public async Task ReloadConfigurationAsync()
        {
            // Сбрасываем кэш
            _cachedBotConfig = null;
            _cachedQuestions = null;
            _cachedPersonalityTypes = null;

            _logger.LogInfo("Configuration cache cleared");

            // Можно опционально перезагрузить сразу
            await GetBotConfigAsync();
            await GetQuestionsAsync();
            await GetPersonalityTypesAsync();
        }

        public async Task<CardGenerationConfig> GetCardGenerationConfigAsync()
        {
            if (_cachedCardGenerationConfig != null)
            {
                return _cachedCardGenerationConfig;
            }

            var configPath = Path.Combine(_configPath, "card-generation.json");

            if (!File.Exists(configPath))
            {
                _logger.LogWarning($"Card generation config not found: {configPath}. Using defaults.");
                _cachedCardGenerationConfig = new CardGenerationConfig();
                return _cachedCardGenerationConfig;
            }

            try
            {
                var json = await File.ReadAllTextAsync(configPath);
                var config = JsonSerializer.Deserialize<CardGenerationConfig>(json, _jsonOptions);

                _cachedCardGenerationConfig = config ?? new CardGenerationConfig();
                _logger.LogDebug($"Loaded card generation config from: {configPath}");

                return _cachedCardGenerationConfig;
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, $"Invalid JSON in card generation config: {configPath}");
                _cachedCardGenerationConfig = new CardGenerationConfig();
                return _cachedCardGenerationConfig;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to load card generation config from {configPath}");
                _cachedCardGenerationConfig = new CardGenerationConfig();
                return _cachedCardGenerationConfig;
            }
        }

        public string GetBasePath()
        {
            // Для Cloud Functions путь будет /function/
            // Для ASP.NET Core - AppContext.BaseDirectory
            // Для разработки - учитываем launchSettings
            var basePath = AppContext.BaseDirectory;

            // Проверяем, не находимся ли мы в bin/Debug/net8.0/
            if (basePath.Contains("bin") && basePath.Contains("Debug"))
            {
                // Поднимаемся на 3 уровня вверх для разработки
                return Path.GetFullPath(Path.Combine(basePath, "../../.."));
            }

            return basePath;
        }

        public async Task<string> GetCardsDirectoryPathAsync()
        {
            var config = await GetCardGenerationConfigAsync();

            // Пробуем несколько вариантов в порядке приоритета:
            var possiblePaths = new List<string>();

            // 1. Явно заданный путь через ASSETS_PATH
            var assetsPathFromEnv = Environment.GetEnvironmentVariable("ASSETS_PATH");
            if (!string.IsNullOrEmpty(assetsPathFromEnv))
            {
                possiblePaths.Add(Path.Combine(assetsPathFromEnv, config.CardsDirectory));
            }

            // 2. Рядом с config (для разработки)
            possiblePaths.Add(Path.Combine(_configPath, "..", "assets", config.CardsDirectory));
            possiblePaths.Add(Path.Combine(_configPath, "..", "..", "assets", config.CardsDirectory));

            // 3. В рабочей директории
            possiblePaths.Add(Path.Combine(Directory.GetCurrentDirectory(), "assets", config.CardsDirectory));

            // 4. В выходной сборке (для Debug)
            var assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (!string.IsNullOrEmpty(assemblyLocation))
            {
                possiblePaths.Add(Path.Combine(assemblyLocation, "assets", config.CardsDirectory));
            }

            // 5. Относительный путь (последний резерв)
            possiblePaths.Add(Path.Combine("assets", config.CardsDirectory));

            foreach (var path in possiblePaths)
            {
                var fullPath = Path.GetFullPath(path);
                _logger.LogDebug($"Checking cards directory: {fullPath}");

                if (Directory.Exists(fullPath))
                {
                    _logger.LogInfo($"Found cards directory: {fullPath}");
                    return fullPath;
                }
            }

            // Если ничего не найдено, создаем
            var fallbackPath = Path.Combine(Directory.GetCurrentDirectory(), "assets", config.CardsDirectory);
            _logger.LogWarning($"Cards directory not found, creating: {fallbackPath}");
            Directory.CreateDirectory(fallbackPath);

            return fallbackPath;
        }

        public string GetAbsolutePath(string relativePath)
        {
            var basePath = GetBasePath();
            var absolutePath = Path.Combine(basePath, relativePath);

            // Нормализуем путь (убираем ../ и т.д.)
            absolutePath = Path.GetFullPath(absolutePath);

            _logger.LogDebug($"Absolute path for '{relativePath}': {absolutePath}");
            return absolutePath;
        }

        private void ValidateQuestions(List<Question> questions)
        {
            var uniqueIds = new HashSet<int>();

            foreach (var question in questions)
            {
                // Проверяем уникальность ID вопросов
                if (!uniqueIds.Add(question.Id))
                {
                    _logger.LogWarning($"Duplicate question ID: {question.Id}");
                }

                // Проверяем ответы
                var uniqueAnswerIds = new HashSet<int>();
                foreach (var answer in question.Answers)
                {
                    if (!uniqueAnswerIds.Add(answer.Id))
                    {
                        _logger.LogWarning($"Question {question.Id}: Duplicate answer ID: {answer.Id}");
                    }
                }
            }
        }

        private BotConfig GetDefaultBotConfig()
        {
            return new BotConfig(
                name: "Профайлинг TecForce",
                welcomeMessage: GetDefaultWelcomeMessage(),
                channelLink: "t.me/jsaland",
                introMessage: GetDefaultIntroMessage(),
                completionMessage: GetDefaultCompletionMessage(),
                totalQuestions: 8,
                answersPerQuestion: 5
            );
        }

        private string GetDefaultWelcomeMessage()
        {
            return "Добро пожаловать! Это тест-ключ к знакомству с собой! Он поможет определить твой тип личности, а эксперт расскажет, что делать с этой информацией :) Если интересно узнать больше, подписывайся на канал t.me/jsland";
        }

        private string GetDefaultIntroMessage()
        {
            return "Добро пожаловать в новый дивный мир! Пройдя тест, ты узнаешь:\n• Свой преобладающий тип личности\n• Сильные стороны и зоны роста\n• Рекомендации по развитию\n\nПоехали!";
        }

        private string GetDefaultCompletionMessage()
        {
            return "🎉 Поздравляем! Вы успешно прошли тест!";
        }
    }
}