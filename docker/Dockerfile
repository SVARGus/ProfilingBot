# Этап 1: Сборка
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Копируем файлы проектов
COPY ["ProfilingBot/src/ProfilingBot.Api/ProfilingBot.Api.csproj", "ProfilingBot/src/ProfilingBot.Api/"]
COPY ["ProfilingBot/src/ProfilingBot.Core/ProfilingBot.Core.csproj", "ProfilingBot/src/ProfilingBot.Core/"]
COPY ["ProfilingBot/src/ProfilingBot.Cloud/ProfilingBot.Cloud.csproj", "ProfilingBot/src/ProfilingBot.Cloud/"]

# Восстанавливаем зависимости (без runtime)
RUN dotnet restore "ProfilingBot/src/ProfilingBot.Api/ProfilingBot.Api.csproj"

# Копируем весь код
COPY . .

# Сборка
WORKDIR "/src/ProfilingBot/src/ProfilingBot.Api"
RUN dotnet build "ProfilingBot.Api.csproj" -c Release -o /app/build

# Публикация (без runtime)
RUN dotnet publish "ProfilingBot.Api.csproj" -c Release -o /app/publish --no-restore

# Этап 2: Финальный образ
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Устанавливаем системные зависимости для SkiaSharp
RUN apt-get update && apt-get install -y \
    libfontconfig1 \
    libfreetype6 \
    libharfbuzz0b \
    libjpeg62-turbo \
    libpng16-16 \
    libssl3 \
    && rm -rf /var/lib/apt/lists/*

# Копируем из этапа сборки
COPY --from=build /app/publish .

# Создаем необходимые директории
RUN mkdir -p /app/config \
    && mkdir -p /app/assets \
    && mkdir -p /app/data/active \
    && mkdir -p /app/data/completed \
    && mkdir -p /app/data/exports \
    && mkdir -p /app/data/logs

# Настраиваем переменные окружения по умолчанию
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://+:5000 \
    CONFIG_PATH=/app/config \
    DATA_PATH=/app/data \
    ASSETS_PATH=/app/assets \
    LOGS_PATH=/app/data/logs

# Открываем порт
EXPOSE 5000

# Запуск
ENTRYPOINT ["dotnet", "ProfilingBot.Api.dll"]