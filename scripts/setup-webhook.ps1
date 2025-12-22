# setup-webhook.ps1 - Настройка вебхука Telegram бота
param(
    [Parameter(Mandatory=$true)]
    [string]$BotToken,
    
    [string]$WebhookUrl,
    
    [switch]$ManualUrl = $false,
    
    [switch]$DeleteOnly = $false
)

$ErrorActionPreference = "Stop"

Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "🌐 НАСТРОЙКА WEBHOOK TELEGRAM BOT" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan

Write-Host "🤖 Проверка бота..." -ForegroundColor Yellow
try {
    $botInfo = Invoke-RestMethod -Uri "https://api.telegram.org/bot$BotToken/getMe" -ErrorAction Stop
    if ($botInfo.ok) {
        Write-Host "   ✅ Бот: @$($botInfo.result.username) (ID: $($botInfo.result.id))" -ForegroundColor Green
    } else {
        Write-Host "   ❌ Ошибка: $($botInfo.description)" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "   ❌ Не удалось подключиться к боту: $_" -ForegroundColor Red
    Write-Host "   Проверьте токен и интернет-соединение" -ForegroundColor Yellow
    exit 1
}

# 1. Удаляем старый вебхук
Write-Host "`n🗑️  Удаление старого вебхука..." -ForegroundColor Yellow
try {
    $deleteResult = Invoke-RestMethod -Uri "https://api.telegram.org/bot$BotToken/deleteWebhook" -Method Post -ErrorAction Stop
    if ($deleteResult.ok) {
        Write-Host "   ✅ Старый вебхук удалён" -ForegroundColor Green
    } else {
        Write-Host "   ⚠️  $($deleteResult.description)" -ForegroundColor Yellow
    }
} catch {
    Write-Host "   ⚠️  Ошибка при удалении: $_" -ForegroundColor Yellow
}

if ($DeleteOnly) {
    Write-Host "`n✅ Режим только удаления завершён" -ForegroundColor Green
    exit 0
}

# 2. Определяем URL для вебхука
if ([string]::IsNullOrEmpty($WebhookUrl) -or $ManualUrl) {
    Write-Host "`n🔗 Введите публичный URL для вебхука:" -ForegroundColor Yellow
    Write-Host "   Пример: https://abc123.lhr.life/api/bot/webhook" -ForegroundColor Gray
    Write-Host "   Получите его из окна SSH-туннеля (localhost.run)" -ForegroundColor Gray
    $WebhookUrl = Read-Host "   URL вебхука"
    
    if ([string]::IsNullOrEmpty($WebhookUrl)) {
        Write-Host "   ❌ URL не может быть пустым" -ForegroundColor Red
        exit 1
    }
}

# 3. Проверяем URL
if (-not $WebhookUrl.StartsWith("https://")) {
    Write-Host "   ⚠️  Внимание: URL должен начинаться с https://" -ForegroundColor Red
    $continue = Read-Host "   Продолжить? (y/N)"
    if ($continue -ne 'y') {
        exit 1
    }
}

Write-Host "`n🌐 Настройка нового вебхука..." -ForegroundColor Yellow
Write-Host "   URL: $WebhookUrl" -ForegroundColor Gray

# 4. Настраиваем вебхук (с очисткой старых сообщений)
$webhookParams = @{
    url = $WebhookUrl
    drop_pending_updates = $true
    max_connections = 20
}

$queryString = ($webhookParams.GetEnumerator() | ForEach-Object { "$($_.Key)=$([System.Web.HttpUtility]::UrlEncode($_.Value))" }) -join '&'
$setWebhookUrl = "https://api.telegram.org/bot$BotToken/setWebhook?$queryString"

try {
    $setResult = Invoke-RestMethod -Uri $setWebhookUrl -Method Post -ErrorAction Stop
    
    if ($setResult.ok) {
        Write-Host "   ✅ Вебхук успешно настроен!" -ForegroundColor Green
    } else {
        Write-Host "   ❌ Ошибка: $($setResult.description)" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "   ❌ Ошибка запроса: $_" -ForegroundColor Red
    exit 1
}

# 5. Проверяем настройки
Write-Host "`n🔍 Проверка настроек вебхука..." -ForegroundColor Yellow
try {
    $checkResult = Invoke-RestMethod -Uri "https://api.telegram.org/bot$BotToken/getWebhookInfo" -Method Get -ErrorAction Stop
    
    if ($checkResult.ok) {
        if ([string]::IsNullOrEmpty($checkResult.result.url)) {
            Write-Host "   ❌ Вебхук не настроен!" -ForegroundColor Red
        } elseif ($checkResult.result.url -ne $WebhookUrl) {
            Write-Host "   ⚠️  Вебхук настроен на другой URL!" -ForegroundColor Yellow
            Write-Host "   Ожидали: $WebhookUrl" -ForegroundColor Gray
            Write-Host "   Получили: $($checkResult.result.url)" -ForegroundColor Gray
        } else {
            Write-Host "   ✅ Вебхук активен" -ForegroundColor Green
            Write-Host "   📊 Ожидающих сообщений: $($checkResult.result.pending_update_count)" -ForegroundColor Gray
            Write-Host "   🔗 Макс. соединений: $($checkResult.result.max_connections)" -ForegroundColor Gray
            
            if ($checkResult.result.last_error_date -gt 0) {
                $errorDate = [DateTimeOffset]::FromUnixTimeSeconds($checkResult.result.last_error_date).ToString("yyyy-MM-dd HH:mm:ss")
                Write-Host "   ⚠️  Последняя ошибка: $($checkResult.result.last_error_message)" -ForegroundColor Red
                Write-Host "   📅 Дата ошибки: $errorDate" -ForegroundColor Gray
            }
        }
    }
} catch {
    Write-Host "   ⚠️  Не удалось проверить вебхук: $_" -ForegroundColor Yellow
}

# 6. Тестовое сообщение (опционально)
Write-Host "`n🧪 Отправка тестового сообщения..." -ForegroundColor Yellow
$testChat = Read-Host "   Отправить тестовое сообщение? Укажите ваш Chat ID или нажмите Enter для пропуска"
if (-not [string]::IsNullOrEmpty($testChat)) {
    try {
        $testMessage = Invoke-RestMethod -Uri "https://api.telegram.org/bot$BotToken/sendMessage?chat_id=$testChat&text=✅ Webhook настроен успешно! Бот готов к работе." -Method Post
        if ($testMessage.ok) {
            Write-Host "   ✅ Тестовое сообщение отправлено" -ForegroundColor Green
        }
    } catch {
        Write-Host "   ⚠️  Не удалось отправить тестовое сообщение: $_" -ForegroundColor Yellow
    }
}

Write-Host "`n=============================================" -ForegroundColor Cyan
Write-Host "✅ НАСТРОЙКА ЗАВЕРШЕНА!" -ForegroundColor Green
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "`n🎯 Теперь можете тестировать бота в Telegram:" -ForegroundColor Green
Write-Host "1. Отправьте /start" -ForegroundColor White
Write-Host "2. Пройдите тест" -ForegroundColor White
Write-Host "3. Проверьте логи в окне сервера" -ForegroundColor White

if ($ManualUrl) {
    Write-Host "`n💡 Сохраните этот URL для повторного использования:" -ForegroundColor Yellow
    Write-Host "   $WebhookUrl" -ForegroundColor Cyan
}