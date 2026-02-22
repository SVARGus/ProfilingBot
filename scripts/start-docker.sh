#!/bin/bash

# Проверяем наличие .env файла
if [ ! -f .env ]; then
    echo "Создаем файл .env из шаблона .env.example"
    cp .env.example .env
    echo "Отредактируйте .env файл и укажите BOT_TOKEN"
    exit 1
fi

# Загружаем переменные окружения
source .env

# Проверяем BOT_TOKEN
if [ -z "$BOT_TOKEN" ] || [ "$BOT_TOKEN" = "your_bot_token_here" ]; then
    echo "Ошибка: BOT_TOKEN не установлен в .env файле"
    exit 1
fi

# Создаем необходимые директории
mkdir -p config assets data/active data/completed data/exports data/logs

echo "Запуск Profiling Bot в Docker..."
docker compose -f docker/docker-compose.yml up -d --build

echo "Запуск завершен!"
echo "Логи: docker compose -f docker/docker-compose.yml logs -f"
echo "Остановка: docker compose -f docker/docker-compose.yml down"
