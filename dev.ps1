# FitTrack Dev Launcher
# Tek komutla backend + frontend'i ayrı terminal pencerelerinde başlatır.
# Kullanım: .\dev.ps1

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$apiDir = Join-Path $root "FitTrack.API"
$clientDir = Join-Path $root "fittrack-client"

Write-Host "FitTrack başlatılıyor..." -ForegroundColor Cyan

# Backend — yeni PowerShell penceresinde
Start-Process powershell -ArgumentList @(
    "-NoExit",
    "-Command",
    "Write-Host 'Backend (:5000) başlatılıyor...' -ForegroundColor Green; cd '$apiDir'; dotnet run --no-launch-profile"
)

# Frontend — yeni PowerShell penceresinde
Start-Process powershell -ArgumentList @(
    "-NoExit",
    "-Command",
    "Write-Host 'Frontend (:5173) başlatılıyor...' -ForegroundColor Magenta; cd '$clientDir'; npm run dev"
)

Write-Host "Backend  → http://localhost:5000" -ForegroundColor Green
Write-Host "Frontend → http://localhost:5173" -ForegroundColor Magenta
Write-Host "İki pencere açıldı. Kapatmak için pencereleri kapat." -ForegroundColor Gray
