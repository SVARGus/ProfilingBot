# Telegram-бот для тестирования типа личности — ProfilingBot

## Техническое задание

### Цель проекта
Telegram-бот для проведения теста из 8 вопросов по определению типа личности с последующим предоставлением результатов и генерацией персонализированной карточки.

### Целевая аудитория
- Возраст: 16-45 лет
- Стиль общения: на "ты", легкий, без сложной терминологии
- Любой пол, образование и опыт работы

### Основной функционал
1. Последовательное прохождение 8 вопросов с 5 вариантами ответов (порядок вопросов и ответов рандомизируется)
2. Автоматический расчёт типа личности (5 типов: Социальный, Творческий, Технический, Аналитический, Натуралистический)
3. Генерация персонализированной карточки с результатами (900x1200 PNG)
4. Возможность пересылки результатов и получения карточки для сторис
5. Админ-панель: статистика, Excel-отчёты, управление администраторами
6. Панель admin+: скачивание логов и JSON данных

### Технические требования
- **Производительность:** до 30 одновременных пользователей
- **Хранение данных:** JSON файлы (файловое хранилище)
- **Конфигурация:** внешние JSON файлы для вопросов, типов личности, настроек и стилизации карточек
- **Архитектура:** ASP.NET Core 8, Docker контейнеризация
- **Время:** все данные записываются в UTC (разница с Москвой +3 часа), в логах — UTC

---

## Дорожная карта разработки

### Этап 1: MVP в Yandex Cloud (завершён, мигрировано)

> Изначально проект разрабатывался как Yandex Cloud Function. Из-за ограничений платформы принято решение о миграции на self-hosted серверное решение. Бизнес-логика, обработчики и сервисы были перенесены в ASP.NET Core Web API.

- [x] Создание бота в @BotFather
- [x] Проектная структура .NET 8
- [x] Система конфигурации из JSON файлов
- [x] Система загрузки вопросов из `questions.json`
- [x] Механизм последовательного опроса с рандомизацией
- [x] Обработка ответов пользователей
- [x] Сохранение сессий в JSON файлы
- [x] Логика расчёта результатов
- [x] Генерация текстовых результатов
- [x] Генерация карточки с результатами (SkiaSharp)

### Этап 2: Self-hosted серверное решение (текущий)

- [x] Создание ASP.NET Core Web API (ProfilingBot.Api)
- [x] Перенос бизнес-логики из Cloud Functions
- [x] Настройка DI контейнера
- [x] Контроллер для вебхука (`POST /api/bot/webhook`)
- [x] Файловая система хранения данных (JSON)
- [x] Dockerfile для контейнеризации
- [x] docker-compose.yml для оркестрации
- [x] Система логгирования (файловый логгер)
- [x] Health check endpoint (`GET /health`)
- [x] Админ-панель для управления (статистика, экспорт, управление админами)
- [x] Экспорт данных в Excel (ClosedXML)
- [x] Скачивание лог-файлов и JSON результатов
- [ ] Настройка Nginx (reverse proxy) + SSL — опционально при деплое
- [ ] Нагрузочное тестирование
- [ ] Финальное тестирование

---

## Структура проекта

```
profiling-bot/
├── ProfilingBot/
│   └── src/
│       ├── ProfilingBot.Api/                          # ASP.NET Core Web API
│       │   ├── Controllers/
│       │   │   └── BotController.cs                   # Контроллер вебхука Telegram
│       │   ├── appsettings.json                       # Настройки логгирования
│       │   └── Program.cs                             # Точка входа, DI, маршруты
│       │
│       ├── ProfilingBot.Cloud/                        # Обработчики Telegram-обновлений
│       │   ├── Handlers/
│       │   │   ├── UpdateHandler.cs                   # Базовый абстрактный класс
│       │   │   ├── AdminCommandHandler.cs             # Обработчик /admin и админ-панели
│       │   │   ├── CommandUpdateHandler.cs            # Обработчик /start, /help
│       │   │   ├── CallbackQueryUpdateHandler.cs      # Обработчик нажатий инлайн-кнопок
│       │   │   └── TextMessageUpdateHandler.cs        # Обработчик текстовых сообщений
│       │   ├── UpdateRouter.cs                        # Маршрутизатор обновлений
│       │   └── ServiceCollectionExtensions.cs         # Регистрация зависимостей (DI)
│       │
│       └── ProfilingBot.Core/                         # Бизнес-логика
│           ├── Helpers/
│           │   └── TimeHelper.cs                      # Конвертация UTC ↔ Москва
│           ├── Services/
│           │   ├── AdminService.cs                    # Управление админами, статистика
│           │   ├── ExcelExportService.cs              # Генерация Excel-отчётов
│           │   ├── FileStorageService.cs              # Хранение сессий (JSON)
│           │   ├── FileConfigurationService.cs        # Загрузка конфигураций
│           │   ├── FileLoggerService.cs               # Файловый логгер
│           │   ├── TestService.cs                     # Расчёт типа личности
│           │   ├── ResultGeneratorService.cs          # Генератор результатов
│           │   └── StoryCardGenerator.cs              # Генерация PNG-карточки (SkiaSharp)
│           ├── Models/
│           │   ├── AdminUser.cs                       # Модель администратора
│           │   ├── AnswerOption.cs                    # Вариант ответа
│           │   ├── Question.cs                        # Вопрос теста
│           │   ├── TestSession.cs                     # Сессия тестирования
│           │   ├── TestResult.cs                      # Результат теста
│           │   ├── PersonalityType.cs                 # Тип личности
│           │   ├── CardGenerationConfig.cs            # Конфигурация карточки
│           │   ├── DailyStats.cs                      # Статистика за день
│           │   └── WeeklyStats.cs                     # Статистика за неделю
│           └── Interfaces/
│               ├── IAdminService.cs                   # Интерфейс админ-сервиса
│               ├── IExportService.cs                  # Интерфейс экспорта Excel
│               ├── ITestService.cs                    # Интерфейс тестирования
│               ├── IResultGeneratorService.cs         # Интерфейс генератора результатов
│               ├── ILoggerService.cs                  # Интерфейс логгера
│               ├── IConfigurationService.cs           # Интерфейс конфигурации
│               ├── IStorageService.cs                 # Интерфейс хранилища
│               └── IStoryCardGenerator.cs             # Интерфейс генератора карточек
│
├── config/                                # Конфигурационные файлы
│   ├── test-config.json                   # Основные настройки бота и теста
│   ├── questions.json                     # Вопросы и варианты ответов
│   ├── personality-types.json             # Типы личности и описания
│   ├── card-generation.json               # Стилизация карточки результата
│   ├── admins.json                        # Список администраторов
│   └── appsettings-bot.json               # Токен бота
│
├── assets/                                # Ресурсы
│   └── cards/                             # Фоновые изображения карточек (1-5.png)
│       ├── 1.png                          # Социальный (ID=1)
│       ├── 2.png                          # Творческий (ID=2)
│       ├── 3.png                          # Технический (ID=3)
│       ├── 4.png                          # Аналитический (ID=4)
│       └── 5.png                          # Натуралистический (ID=5)
│
├── data/                                  # Данные (создаётся автоматически)
│   ├── active/                            # Активные сессии
│   │   └── active-sessions.json
│   ├── completed/                         # Завершённые сессии
│   │   └── completed-sessions.json
│   ├── exports/                           # Сгенерированные Excel-отчёты
│   └── logs/                              # Логи приложения (bot_YYYYMMDD.log)
│
├── docker/                                # Docker-конфигурация
│   ├── Dockerfile                         # Multi-stage build (SDK 8.0 → ASP.NET 8.0)
│   ├── docker-compose.yml                 # Production-конфигурация
│   └── docker-compose.dev.yml             # Development-конфигурация
│
├── scripts/                               # Скрипты запуска
│   ├── start-dev.ps1                      # Запуск для разработки (Windows)
│   ├── start-docker.sh                    # Запуск через Docker (Linux)
│   ├── update-docker.sh                   # Обновление Docker-контейнера
│   └── setup-webhook.ps1                  # Настройка вебхука Telegram
│
├── .env.example                           # Шаблон переменных окружения
├── .env.production                        # Переменные для продакшена
├── .env.development.local                 # Переменные для разработки (в .gitignore)
│
├── tests/                                 # Тесты
├── docs/                                  # Дополнительная документация
├── LICENSE.txt
└── README.md
```

---

## Конфигурационные файлы

### 1. test-config.json — основные настройки бота

```json
{
  "name": "Тест TecForce",
  "welcomeMessage": "Добро пожаловать! Это тест-ключ к знакомству с собой! Он поможет определить твой тип личности, а эксперт расскажет, что делать с этой информацией :) Если интересно узнать больше, подписывайся на канал t.me/jsaland",
  "channelLink": "t.me/jsaland",
  "introMessage": "Добро пожаловать в новый дивный мир! Пройдя тест, ты узнаешь:\n• Свой преобладающий тип личности\n• Сильные стороны и зоны роста\n• Рекомендации по развитию\n\nПоехали!",
  "completionMessage": "Поздравляем! Вы успешно прошли тест!",
  "totalQuestions": 8,
  "answersPerQuestion": 5
}
```

**Описание полей:**
| Поле | Описание |
|------|----------|
| `name` | Название теста (отображается в информации о боте) |
| `welcomeMessage` | Приветственное сообщение при команде /start |
| `channelLink` | Ссылка на Telegram-канал |
| `introMessage` | Сообщение перед началом теста |
| `completionMessage` | Сообщение при завершении теста |
| `totalQuestions` | Количество вопросов в тесте |
| `answersPerQuestion` | Количество вариантов ответа на каждый вопрос |

### 2. questions.json — вопросы и варианты ответов

```json
[
  {
    "id": 1,
    "text": "Чем вы предпочитаете заниматься в свободное время?",
    "answers": [
      {
        "id": 1,
        "text": "Встречаться с друзьями, родными, устраивать совместные мероприятия...",
        "idPersonalityType": 1
      },
      {
        "id": 2,
        "text": "Посвящать время искусству через коллекционирование, рисование...",
        "idPersonalityType": 2
      },
      {
        "id": 3,
        "text": "Разбирать, собирать, чинить технику/электронику...",
        "idPersonalityType": 3
      },
      {
        "id": 4,
        "text": "Читать нон-фикшн, решать логические задачки...",
        "idPersonalityType": 4
      },
      {
        "id": 5,
        "text": "Проводить время на природе, ухаживать за растениями/животными...",
        "idPersonalityType": 5
      }
    ]
  }
]
```

**Описание полей:**
| Поле | Описание |
|------|----------|
| `id` | Уникальный идентификатор вопроса (1-8) |
| `text` | Текст вопроса |
| `answers[].id` | Идентификатор варианта ответа (1-5) |
| `answers[].text` | Текст варианта ответа |
| `answers[].idPersonalityType` | ID типа личности, к которому относится ответ (1-5) |

> **Примечание:** порядок вопросов и вариантов ответов рандомизируется при каждом прохождении теста.

### 3. personality-types.json — типы личности

```json
[
  {
    "id": 1,
    "name": "Социальный",
    "shortName": "[ КОММУНИКАТОР ]",
    "slogan": "легко находит общий язык и объединяет людей",
    "fullName": "Социальный тип — Человек-коммуникация",
    "description": "Вы легко находите общий язык с людьми...",
    "strengths": "Ваша сила — люди и связи.",
    "sphere": "Ваша стихия: проекты, где важны коммуникации...",
    "recommendations": "Ищите роли, где ценится умение договариваться...",
    "imagePath": "images/social.png",
    "shareTemplate": "Я - Социальный тип! Узнай свой тип личности: {link}"
  }
]
```

**Типы личности (5 типов):**

| ID | Название | Короткое | Описание |
|----|----------|----------|----------|
| 1 | Социальный | КОММУНИКАТОР | Человек-коммуникация, объединяет людей |
| 2 | Творческий | ТВОРЕЦ | Человек-идея, генерирует креатив |
| 3 | Технический | ТЕХНИК | Человек-система, решает технические задачи |
| 4 | Аналитический | АНАЛИТИК | Человек-логика, структурное мышление |
| 5 | Натуралистический | НАТУРАЛИСТ | Человек-природа, забота и гармония |

### 4. card-generation.json — стилизация карточки результата

```json
{
  "width": 900,
  "height": 1200,
  "blockMarginLeft": 60,
  "blockMarginTop": 50,
  "blockMaxWidth": 600,
  "cardsDirectory": "cards",
  "userNameConfig": {
    "fontFamily": "Inter",
    "fontStyle": 0,
    "fontSize": 32,
    "letterSpacingPercent": 2,
    "colorHex": "#FFFFFF",
    "yOffset": 0
  },
  "shortNameConfig": {
    "fontFamily": "Inter",
    "fontStyle": 1,
    "fontSize": 40,
    "letterSpacingPercent": 10,
    "colorHex": "#DCE6F2",
    "yOffset": 0
  },
  "sloganConfig": {
    "fontFamily": "Inter",
    "fontStyle": 0,
    "fontSize": 32,
    "letterSpacingPercent": 0,
    "colorHex": "#AAB6C4",
    "yOffset": 0
  },
  "shortNameLineConfig": {
    "colorHex": "#DCE6F2",
    "opacity": 0.7,
    "strokeWidth": 2,
    "verticalOffset": 15
  }
}
```

Карточка генерируется путём наложения текста (имя пользователя, тип личности, слоган) на фоновое изображение из `assets/cards/{id}.png`.

### 5. admins.json — список администраторов

```json
[
  {
    "UserId": 0,
    "UserName": "@OwnerUsername",
    "Role": "owner",
    "AddedAt": "2026-01-01T00:00:00Z",
    "AddedBy": "system",
    "IsOwner": true,
    "CanManageAdmins": true
  },
  {
    "UserId": 0,
    "UserName": "@AdminUsername",
    "Role": "admin",
    "AddedAt": "2026-01-01T00:00:00Z",
    "AddedBy": "system",
    "IsOwner": false,
    "CanManageAdmins": false
  }
]
```

**Описание полей:**
| Поле | Описание |
|------|----------|
| `UserId` | Telegram ID пользователя (0 — заполнится автоматически при первом обращении) |
| `UserName` | Telegram username с символом @ |
| `Role` | Роль: `"owner"` или `"admin"` |
| `AddedAt` | Дата добавления (UTC) |
| `AddedBy` | Кем добавлен (`"system"` или username владельца) |
| `IsOwner` | Является ли владельцем |
| `CanManageAdmins` | Может ли управлять списком админов (только owner) |

> **Примечание:** при первом взаимодействии пользователя с ботом система автоматически подставит его `UserId` по совпадению `UserName`.

### 6. appsettings-bot.json — токен бота

```json
{
  "BotToken": "ВАШ_ТОКЕН_БОТА"
}
```

> **Внимание:** этот файл содержит секретный токен. Не добавляйте его в публичный репозиторий. При деплое через Docker используйте переменную окружения `BOT_TOKEN` вместо этого файла.

### 7. .env — переменные окружения

```env
# Telegram Bot Token (обязательно)
BOT_TOKEN=your_bot_token_here

# Пути к директориям (не менять при Docker-деплое)
CONFIG_PATH=/app/config
DATA_PATH=/app/data
ASSETS_PATH=/app/assets
LOGS_PATH=/app/data/logs

# ASP.NET Core
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:5000
```

---

## Функционал админ-панели

Доступ к админ-панели по команде `/admin`.

### Роль "admin" — администратор

| Функция | Описание |
|---------|----------|
| Статистика за день | Количество тестов, распределение по типам личности |
| Статистика за 7 дней | Аналогично, за последнюю неделю |
| Excel-отчёт (всё время) | Скачать все результаты тестов в Excel |
| Excel-отчёт (30 дней) | Скачать результаты за последние 30 дней |
| Информация о боте | Название, username, ID, количество тестов, активные сессии |

### Роль "owner" — владелец (admin + дополнительные функции)

| Функция | Описание |
|---------|----------|
| Все функции admin | Статистика, экспорт, информация |
| Список админов | Просмотр всех администраторов с ролями |
| Добавить админа | Добавление нового администратора по username |
| Удалить админа | Удаление администратора с подтверждением |
| Скачать логи | Лог-файлы за последние 7 дней |
| Скачать JSON | Файл completed-sessions.json с результатами |

### Процесс добавления админа
1. Владелец нажимает "Добавить админа"
2. Бот запрашивает username нового админа
3. Ввод username (минимум 3 символа, таймаут 5 минут)
4. Админ добавляется с `UserId=0`
5. При первом обращении нового админа к боту его ID обновится автоматически

---

## User Flow тестирования

### Шаг 1: Старт
```
Пользователь: /start
Бот: "Добро пожаловать! Это тест-ключ к знакомству с собой!
      Он поможет определить твой тип личности, а эксперт расскажет,
      что делать с этой информацией :)
      Если интересно узнать больше, подписывайся на канал t.me/jsaland"

      [Кнопка "Старт"]
```

### Шаг 2: Введение
```
Пользователь: Нажимает "Старт"
Бот: "Добро пожаловать в новый дивный мир!
      Пройдя тест, ты узнаешь:
      • Свой преобладающий тип личности
      • Сильные стороны и зоны роста
      • Рекомендации по развитию
      Поехали!"

      [Кнопка "Начать тест"]
```

### Шаг 3: Вопросы 1-8
```
Бот: "Вопрос 1/8: [Текст вопроса]"

      [Кнопка "Вариант 1"]   (порядок
      [Кнопка "Вариант 2"]    рандомизирован)
      [Кнопка "Вариант 3"]
      [Кнопка "Вариант 4"]
      [Кнопка "Вариант 5"]
```

### Шаг 4: Результат
```
Бот: "Поздравляем! Вы успешно прошли тест!

      [Картинка — фон типа личности]

      Ваш тип личности: [Тип]
      [Развёрнутое описание]
      [Сильные стороны]
      [Сфера]
      [Рекомендации]

      [Кнопка "Получить карточку с результатом"]
      [Кнопка "Пройти тест заново"]"
```

### Доступные команды
| Команда | Описание |
|---------|----------|
| `/start` | Начать взаимодействие с ботом |
| `/help` | Справка по командам и возможностям |
| `/admin` | Открыть админ-панель (только для администраторов) |

---

## Сбор и анализ данных

### Структура данных сессии (completed-sessions.json)

```json
{
  "Id": "40ac67dc-92fb-4dde-872d-2d7beed50744",
  "UserId": 123456789,
  "UserName": "@username",
  "StartedAt": "2026-02-04T20:56:30.869Z",
  "CompletedAt": "2026-02-04T20:56:46.216Z",
  "CurrentQuestionIndex": 8,
  "Answers": {
    "1": 4,
    "2": 3,
    "3": 2,
    "4": 5,
    "5": 1,
    "6": 3,
    "7": 4,
    "8": 2
  },
  "ResultIdPersonalityType": 4,
  "ResultNamePersonalityType": "Аналитический",
  "QuestionOrder": [7, 1, 6, 3, 5, 8, 2, 4],
  "AnswerOrder": {
    "1": [4, 5, 1, 3, 2],
    "2": [3, 1, 5, 2, 4]
  }
}
```

**Описание полей:**
| Поле | Описание |
|------|----------|
| `Id` | UUID сессии |
| `UserId` | Telegram ID пользователя |
| `UserName` | Username пользователя |
| `StartedAt` / `CompletedAt` | Время начала и завершения (UTC) |
| `Answers` | Ответы: `{ID_вопроса: ID_ответа}` |
| `ResultIdPersonalityType` | ID определённого типа личности (1-5) |
| `ResultNamePersonalityType` | Название типа личности |
| `QuestionOrder` | Порядок показа вопросов (рандомизация) |
| `AnswerOrder` | Порядок показа ответов для каждого вопроса |

### Excel-отчёт включает:
- Дата и время прохождения
- ID и имя пользователя Telegram
- Определённый тип личности
- Номер ответа по каждому из 8 вопросов
- Время прохождения теста

---

## Инструкции по развёртыванию

### Предварительные требования

1. **Сервер** с Linux и установленным Docker + Docker Compose
2. **Telegram Bot Token** — получить у [@BotFather](https://t.me/BotFather)
3. **Доменное имя** (опционально, для HTTPS) — привязанное к IP сервера

---

### Шаг 1: Создание и настройка бота в Telegram

#### 1.1. Создание бота

1. Откройте [@BotFather](https://t.me/BotFather) в Telegram
2. Отправьте команду `/newbot`
3. Введите **отображаемое имя** бота (например: `Тест типа личности`)
4. Введите **username** бота (латиницей, должен заканчиваться на `bot`, например: `profiling_test_bot`)
5. BotFather вернёт **токен** вида `123456789:ABCDefGhIjKlMnOpQrStUvWxYz` — сохраните его

#### 1.2. Установка аватарки бота

1. Откройте [@BotFather](https://t.me/BotFather)
2. Отправьте команду `/setuserpic`
3. Выберите вашего бота из списка
4. Отправьте изображение для аватарки

**Требования к аватарке:**
- Формат: JPG или PNG
- Рекомендуемый размер: **512x512 пикселей** (квадратное изображение)
- Минимальный размер: 160x160 пикселей
- Telegram автоматически обрежет изображение в круг при отображении — учитывайте это при дизайне (важные элементы должны быть ближе к центру)

#### 1.3. Настройка описания бота

```
/setdescription — описание, которое видят пользователи при первом открытии бота
                  (до того как нажмут "Старт")
```
Пример: `Пройди тест из 8 вопросов и узнай свой тип личности! Результат — персональная карточка с рекомендациями.`

```
/setabouttext — краткое описание в профиле бота
```
Пример: `Бот для определения типа личности. Тест из 8 вопросов с персонализированными результатами.`

#### 1.4. Настройка списка команд

Отправьте BotFather команду `/setcommands`, выберите вашего бота и отправьте:

```
start - Начать работу с ботом
help - Справка по командам
admin - Админ-панель (для администраторов)
```

Это позволит пользователям видеть список доступных команд при нажатии кнопки `/` в чате с ботом.

#### 1.5. Дополнительные настройки (опционально)

| Команда BotFather | Описание |
|-------------------|----------|
| `/setname` | Изменить отображаемое имя бота |
| `/setuserpic` | Изменить аватарку |
| `/setdescription` | Описание при первом открытии |
| `/setabouttext` | Краткое описание в профиле |
| `/setcommands` | Список команд |
| `/setinline` | Включить инлайн-режим (не используется) |
| `/setprivacy` | Режим приватности в группах (по умолчанию бот видит только команды) |

---

### Шаг 2: Подготовка сервера

```bash
# Установка Docker (если ещё не установлен)
curl -fsSL https://get.docker.com -o get-docker.sh
sudo sh get-docker.sh

# Добавление текущего пользователя в группу docker (чтобы не использовать sudo)
sudo usermod -aG docker $USER
# После этого нужно перелогиниться или выполнить: newgrp docker

# Установка Docker Compose
sudo apt-get install -y docker-compose-plugin

# Проверка установки
docker --version
docker compose version
```

### Шаг 3: Клонирование и настройка проекта

```bash
# Клонирование репозитория
git clone <URL_РЕПОЗИТОРИЯ>
cd profiling-bot

# Создание файла переменных окружения
cp .env.example .env

# Редактирование .env — ОБЯЗАТЕЛЬНО укажите BOT_TOKEN
nano .env
```

Содержимое `.env`:
```env
BOT_TOKEN=123456789:ABCDefGhIjKlMnOpQrStUvWxYz    # Ваш токен от BotFather
CONFIG_PATH=/app/config
DATA_PATH=/app/data
ASSETS_PATH=/app/assets
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:5000
```

### Шаг 4: Настройка конфигурации

Отредактируйте файлы в папке `config/`:

1. **admins.json** — укажите username владельца и администраторов:
```json
[
  {
    "UserId": 0,
    "UserName": "@ВашUsername",
    "Role": "owner",
    "AddedAt": "2026-01-01T00:00:00Z",
    "AddedBy": "system",
    "IsOwner": true,
    "CanManageAdmins": true
  }
]
```

2. **test-config.json** — при необходимости измените приветственные сообщения и ссылку на канал

3. **questions.json** и **personality-types.json** — при необходимости скорректируйте вопросы и описания типов

4. **Карточки** — убедитесь, что в `assets/cards/` находятся фоновые изображения (900x1200 PNG) с именами `1.png` ... `5.png` (по одному на каждый тип личности)

### Шаг 5: Запуск

```bash
# Создание необходимых директорий
mkdir -p data/active data/completed data/exports data/logs

# Сборка и запуск
docker compose -f docker/docker-compose.yml up -d --build

# Проверка статуса
docker compose -f docker/docker-compose.yml ps

# Проверка логов
docker compose -f docker/docker-compose.yml logs -f
```

### Шаг 6: Настройка вебхука Telegram

После запуска контейнера нужно зарегистрировать вебхук — URL, на который Telegram будет отправлять обновления.

**Вариант A — с доменом и SSL (рекомендуется для production):**
```bash
curl -F "url=https://ваш-домен.ru/api/bot/webhook" \
     https://api.telegram.org/bot<BOT_TOKEN>/setWebhook
```

**Вариант B — с SSH-туннелем (для тестирования):**
```bash
# Запустите SSH-туннель
ssh -R 80:localhost:5000 nokey@localhost.run

# Используйте полученный URL
curl -F "url=https://xxxxx.lhr.life/api/bot/webhook" \
     https://api.telegram.org/bot<BOT_TOKEN>/setWebhook
```

**Проверка вебхука:**
```bash
curl https://api.telegram.org/bot<BOT_TOKEN>/getWebhookInfo
```

### Шаг 7: Проверка работоспособности

```bash
# Health check
curl http://localhost:5000/health
```

Отправьте `/start` вашему боту в Telegram — он должен ответить приветственным сообщением.

---

## Настройка HTTPS с доменом (опционально)

Для production-окружения рекомендуется настроить HTTPS через Nginx reverse proxy и Let's Encrypt.

### Установка Nginx

```bash
sudo apt-get update
sudo apt-get install -y nginx
```

### Конфигурация Nginx

Создайте файл `/etc/nginx/sites-available/profiling-bot`:

```nginx
server {
    listen 80;
    server_name ваш-домен.ru;

    location / {
        proxy_pass http://127.0.0.1:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_cache_bypass $http_upgrade;
    }
}
```

```bash
# Активация конфигурации
sudo ln -s /etc/nginx/sites-available/profiling-bot /etc/nginx/sites-enabled/
sudo nginx -t
sudo systemctl reload nginx
```

### Получение SSL-сертификата (Let's Encrypt)

```bash
# Установка Certbot
sudo apt-get install -y certbot python3-certbot-nginx

# Получение сертификата (автоматически настроит Nginx)
sudo certbot --nginx -d ваш-домен.ru

# Проверка автоматического обновления
sudo certbot renew --dry-run
```

После получения сертификата обновите вебхук:
```bash
curl -F "url=https://ваш-домен.ru/api/bot/webhook" \
     https://api.telegram.org/bot<BOT_TOKEN>/setWebhook
```

---

## Управление сервером

### Основные команды

```bash
# Просмотр логов контейнера
docker compose -f docker/docker-compose.yml logs -f

# Перезапуск бота
docker compose -f docker/docker-compose.yml restart

# Остановка бота
docker compose -f docker/docker-compose.yml down

# Запуск бота
docker compose -f docker/docker-compose.yml up -d
```

### Обновление бота

```bash
# Получить обновления из репозитория
git pull

# Пересобрать и перезапустить
docker compose -f docker/docker-compose.yml up -d --build
```

Или используйте готовый скрипт:
```bash
./scripts/update-docker.sh
```

### Резервное копирование данных

```bash
# Создание бекапа данных и конфигов
tar -czf backup_$(date +%Y%m%d).tar.gz data/ config/

# Восстановление из бекапа
tar -xzf backup_YYYYMMDD.tar.gz
```

### Мониторинг

| Endpoint | Описание |
|----------|----------|
| `GET /health` | Проверка работоспособности сервиса |

Логи приложения сохраняются в `data/logs/bot_YYYYMMDD.log` (UTC-время).
Лог-файлы также можно скачать через админ-панель бота (команда `/admin`).

---

## Технические детали реализации

### Архитектура

Приложение построено на трёхслойной архитектуре:

1. **ProfilingBot.Api** — ASP.NET Core Web API, точка входа. Принимает вебхук от Telegram и передаёт обработку в UpdateRouter.
2. **ProfilingBot.Cloud** — слой обработчиков Telegram-обновлений. Маршрутизация по типу обновления с приоритетами:
   - `AdminCommandHandler` (высший приоритет)
   - `CallbackQueryUpdateHandler`
   - `CommandUpdateHandler`
   - `TextMessageUpdateHandler` (низший приоритет)
3. **ProfilingBot.Core** — бизнес-логика, модели, сервисы, интерфейсы.

### Ключевые сервисы

**AdminService** — управление администраторами и статистикой:
- Проверка ролей (admin/owner)
- Добавление/удаление админов
- Автообнаружение UserId по UserName
- Кэширование списка админов (5 минут)
- Генерация статистики (день/неделя)

**FileStorageService** — потокобезопасное хранение сессий:
- Активные сессии: `data/active/active-sessions.json`
- Завершённые сессии: `data/completed/completed-sessions.json`
- Блокировки для thread-safety

**ExcelExportService** — генерация отчётов (ClosedXML):
- Экспорт всех результатов или за период
- Колонки: дата, пользователь, тип личности, ответы по 8 вопросам

**StoryCardGenerator** — генерация PNG-карточек (SkiaSharp):
- Наложение текста на фоновое изображение
- Стилизация через `card-generation.json`
- Шрифты: Inter (встроенные как embedded ресурсы)

**FileLoggerService** — файловый логгер:
- Ежедневные файлы: `bot_YYYYMMDD.log`
- Уровни: Info, Warning, Error, Debug
- Формат: `[HH:mm:ss] [LEVEL] [UserId @username] Сообщение`

---

## Полезные ссылки

- Telegram Bot API: https://core.telegram.org/bots/api
- BotFather: https://t.me/BotFather
- ASP.NET Core: https://docs.microsoft.com/aspnet/core
- Docker: https://docs.docker.com/
- ClosedXML (Excel): https://github.com/ClosedXML/ClosedXML
- SkiaSharp (графика): https://github.com/mono/SkiaSharp

---

*Документация обновлена: 2026-02-21*
*Версия: 2.0.0*
*Статус: Активная разработка*
