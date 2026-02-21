#!/bin/bash

echo "Остановка контейнера..."
docker compose -f docker/docker-compose.yml down

echo "Пересборка образа..."
docker compose -f docker/docker-compose.yml build --no-cache

echo "Запуск..."
docker compose -f docker/docker-compose.yml up -d

echo "Обновление завершено!"
