# start-dev.ps1 - Запуск сервера и SSH-туннеля для разработки
param(
    [string]$BotToken = $env:BOT_TOKEN,
    [switch]$SetupWebhookOnly = $false
)

$ErrorActionPreference = "Stop"

# Конфигурация
$ProjectPath = "ProfilingBot\src\ProfilingBot.Api"
$ServerPort = 5000

Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "🚀 ЗАПУСК СРЕДЫ РАЗРАБОТКИ PROFILING BOT" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan

# Проверка наличия BOT_TOKEN
if ([string]::IsNullOrEmpty($BotToken)) {
    Write-Host "❌ BOT_TOKEN не найден!" -ForegroundColor Red
    Write-Host "Установите одним из способов:" -ForegroundColor Yellow
    Write-Host "1. Переменная окружения: `$env:BOT_TOKEN = 'ВАШ_ТОКЕН'" -ForegroundColor Gray
    Write-Host "2. Параметр скрипта: .\start-dev.ps1 -BotToken 'ВАШ_ТОКЕН'" -ForegroundColor Gray
    Write-Host "3. User Secrets: dotnet user-secrets set 'BOT_TOKEN' 'ВАШ_ТОКЕН'" -ForegroundColor Gray
    exit 1
}

Write-Host "✅ Токен бота: $($BotToken.Substring(0, 10))..." -ForegroundColor Green
Write-Host "📁 Проект: $ProjectPath" -ForegroundColor Gray
Write-Host "🔌 Порт: $ServerPort" -ForegroundColor Gray

if (-not $SetupWebhookOnly) {
    # 1. Проверяем, запущен ли уже сервер
    $serverProcess = Get-Process -Name "dotnet" -ErrorAction SilentlyContinue | 
        Where-Object { $_.CommandLine -like "*$ProjectPath*" }
    
    if ($serverProcess) {
        Write-Host "⚠️  Сервер уже запущен (PID: $($serverProcess.Id))" -ForegroundColor Yellow
        $stopServer = Read-Host "Остановить и перезапустить? (y/N)"
        if ($stopServer -eq 'y') {
            $serverProcess | Stop-Process -Force
            Start-Sleep -Seconds 2
        } else {
            Write-Host "Продолжаем с существующим сервером" -ForegroundColor Yellow
        }
    }
    
    # 2. Запускаем сервер в новом окне
    Write-Host "`n1️⃣  Запуск ASP.NET Core сервера..." -ForegroundColor Yellow
    $serverJob = Start-Job -Name "ProfilingBotServer" -ScriptBlock {
        param($path)
        cd $path
        dotnet run
    } -ArgumentList $PSScriptRoot\..\$ProjectPath
    
    Write-Host "   ✅ Сервер запущен (Job ID: $($serverJob.Id))" -ForegroundColor Green
    Write-Host "   📍 Локальный URL: http://localhost:$ServerPort" -ForegroundColor Gray
    Write-Host "   📍 Health check: http://localhost:$ServerPort/health" -ForegroundColor Gray
    
    # 3. Ждём запуска сервера
    Write-Host "   ⏳ Ожидаем запуска сервера (10 сек)..." -ForegroundColor Gray
    Start-Sleep -Seconds 10
    
    # 4. Проверяем доступность сервера
    try {
        $healthResponse = Invoke-WebRequest "http://localhost:$ServerPort/health" -TimeoutSec 5
        if ($healthResponse.StatusCode -eq 200) {
            Write-Host "   ✅ Сервер готов" -ForegroundColor Green
        }
    } catch {
        Write-Host "   ⚠️  Сервер не ответил на health check" -ForegroundColor Yellow
        Write-Host "   Проверьте окно с сервером вручную" -ForegroundColor Gray
    }
    
    # 5. Запускаем SSH-туннель в новом окне
    Write-Host "`n2️⃣  Запуск SSH-туннеля (localhost.run)..." -ForegroundColor Yellow
    Write-Host "   Откроется новое окно PowerShell" -ForegroundColor Gray
    Write-Host "   ❗ НЕ ЗАКРЫВАЙТЕ это окно во время работы!" -ForegroundColor Red
    
    $tunnelScript = @"
        Write-Host "🌐 SSH-туннель localhost.run" -ForegroundColor Cyan
        Write-Host "Порт $ServerPort → публичный URL" -ForegroundColor Gray
        Write-Host "`nДля остановки нажмите Ctrl+C" -ForegroundColor Yellow
        Write-Host "=====================================" -ForegroundColor Gray
        ssh -o ConnectTimeout=30 -o ServerAliveInterval=60 -R 80:localhost:$ServerPort localhost.run
"@
    
    Start-Process powershell.exe -ArgumentList "-NoExit", "-Command", $tunnelScript
    
    Write-Host "   ✅ Туннель запущен" -ForegroundColor Green
    Write-Host "   📍 Скопируйте URL из нового окна" -ForegroundColor Gray
}

# 6. Запускаем настройку вебхука (отдельный скрипт)
Write-Host "`n3️⃣  Настройка вебхука..." -ForegroundColor Yellow
Write-Host "   Запуск setup-webhook.ps1" -ForegroundColor Gray

$setupScript = Join-Path $PSScriptRoot "setup-webhook.ps1"
if (Test-Path $setupScript) {
    & $setupScript -BotToken $BotToken -ManualUrl $true
} else {
    Write-Host "   ❌ Скрипт setup-webhook.ps1 не найден!" -ForegroundColor Red
    Write-Host "   Создайте его или настройте вебхук вручную" -ForegroundColor Yellow
}

Write-Host "`n=============================================" -ForegroundColor Cyan
Write-Host "✅ Среда разработки готова!" -ForegroundColor Green
Write-Host "=============================================" -ForegroundColor Cyan

if (-not $SetupWebhookOnly) {
    Write-Host "`n📋 ОТКРЫТЫЕ ОКНА:" -ForegroundColor Yellow
    Write-Host "1. Сервер ASP.NET Core (Job)" -ForegroundColor Gray
    Write-Host "2. SSH-туннель localhost.run" -ForegroundColor Gray
    Write-Host "3. Это окно (управление)" -ForegroundColor Gray
    
    Write-Host "`n🎯 КОМАНДЫ УПРАВЛЕНИЯ:" -ForegroundColor Yellow
    Write-Host "• Остановить сервер: Stop-Job -Name ProfilingBotServer" -ForegroundColor Gray
    Write-Host "• Просмотр логов: Receive-Job -Name ProfilingBotServer" -ForegroundColor Gray
    Write-Host "• Перезапуск вебхука: .\start-dev.ps1 -SetupWebhookOnly" -ForegroundColor Gray
}

Write-Host "`n💡 Теперь откройте Telegram и тестируйте бота!" -ForegroundColor Green