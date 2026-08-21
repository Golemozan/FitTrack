# FitTrack Dev Launcher
# WDAC/SAC atlatmak icin: ProgramData'ya publish + self-signed cert ile imzala + exe'den calistir
# Kullanim: .\dev.ps1

$ErrorActionPreference = "Continue"
$root = $PSScriptRoot
$apiDir = Join-Path $root "FitTrack.API"
$clientDir = Join-Path $root "fittrack-client"
$publishDir = Join-Path $env:ProgramData "FitTrack"
$certSubject = "CN=FitTrack Dev"

Write-Host "`n============================" -ForegroundColor Cyan
Write-Host "  FitTrack Baslatiliyor" -ForegroundColor Cyan
Write-Host "============================`n" -ForegroundColor Cyan

# -- 1. Self-signed cert olustur / bul ve trust et --
Write-Host "[1/4] Kod imzalama sertifikasi hazirlaniyor..." -ForegroundColor Gray
$cert = Get-ChildItem Cert:\CurrentUser\My | Where-Object Subject -like "*FitTrack*" | Select-Object -First 1
if (-not $cert) {
    $cert = New-SelfSignedCertificate -Type CodeSigningCert -Subject $certSubject -KeyUsage DigitalSignature -CertStoreLocation Cert:\CurrentUser\My -ErrorAction Stop
    Write-Host "  Yeni cert olusturuldu." -ForegroundColor DarkGray
}

# Trusted Root'a ekle (yoksa)
$rootStore = New-Object System.Security.Cryptography.X509Certificates.X509Store("Root", "CurrentUser")
$rootStore.Open("ReadWrite")
$found = $rootStore.Certificates | Where-Object Thumbprint -eq $cert.Thumbprint
if (-not $found) {
    $rootStore.Add($cert)
    Write-Host "  Cert Trusted Root'a eklendi." -ForegroundColor DarkGray
}
$rootStore.Close()

# Trusted Publisher'a ekle (yoksa)
$tpStore = New-Object System.Security.Cryptography.X509Certificates.X509Store("TrustedPublisher", "CurrentUser")
$tpStore.Open("ReadWrite")
$found = $tpStore.Certificates | Where-Object Thumbprint -eq $cert.Thumbprint
if (-not $found) {
    $tpStore.Add($cert)
    Write-Host "  Cert Trusted Publisher'a eklendi." -ForegroundColor DarkGray
}
$tpStore.Close()

Write-Host "  Cert hazir: $($cert.Thumbprint)" -ForegroundColor Green

# -- 2. Backend publish --
Write-Host "`n[2/4] Backend publish yapiliyor ($publishDir)..." -ForegroundColor Gray

# Eski publish'i temizle (ama DB'yi koru!)
$dbBackup = $null
$oldDb = Join-Path $publishDir "fittrack.db"
if ((Test-Path $oldDb) -and ((Get-Item $oldDb).Length -gt 0)) {
    $dbBackup = Join-Path $env:TEMP "fittrack-backup.db"
    Copy-Item $oldDb $dbBackup -Force
    Write-Host "  Veritabani yedeklendi ($dbBackup)." -ForegroundColor DarkGray
}
if (Test-Path $publishDir) {
    Remove-Item -Recurse -Force $publishDir -ErrorAction SilentlyContinue
}

# Publish
Push-Location $apiDir
try {
    dotnet publish -c Debug -o $publishDir --no-self-contained 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "`n!!! Publish HATASI !!!" -ForegroundColor Red
        Read-Host "Cikmak icin Enter"
        Pop-Location
        exit 1
    }
    Write-Host "  Publish basarili." -ForegroundColor Green
} finally {
    Pop-Location
}

# DB yedekten geri yukle
if ($dbBackup -and (Test-Path $dbBackup)) {
    Copy-Item $dbBackup (Join-Path $publishDir "fittrack.db") -Force
    Remove-Item $dbBackup -Force
    Write-Host "  Veritabani geri yuklendi." -ForegroundColor Green
}

# -- 3. Tum DLL'leri imzala --
Write-Host "`n[3/4] DLL'ler imzalaniyor..." -ForegroundColor Gray
$signed = 0
Get-ChildItem $publishDir -Filter "*.dll" -ErrorAction SilentlyContinue | ForEach-Object {
    $sig = Set-AuthenticodeSignature -FilePath $_.FullName -Certificate $cert -HashAlgorithm SHA256 -Force -ErrorAction SilentlyContinue
    if ($sig.Status -eq "Valid") { $signed++ }
}
Write-Host "  $signed DLL imzalandi." -ForegroundColor Green

# Eski process'i oldur
$portProc = Get-NetTCPConnection -LocalPort 5000 -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess -Unique
if ($portProc) {
    $portProc | ForEach-Object { Stop-Process -Id $_ -Force -ErrorAction SilentlyContinue }
    Write-Host "  Port 5000 bosaltildi." -ForegroundColor DarkGray
}

# Frontend bagimlilik kontrol
if (-not (Test-Path (Join-Path $clientDir "node_modules"))) {
    Write-Host "  node_modules bulunamadi, npm install yapiliyor..." -ForegroundColor Yellow
    Push-Location $clientDir
    try { npm install 2>&1 | Out-Null } finally { Pop-Location }
}

# -- 4. Backend + Frontend baslat --
Write-Host "`n[4/4] Uygulama baslatiliyor...`n" -ForegroundColor Gray
Write-Host "  Backend  : http://localhost:5000" -ForegroundColor Green
Write-Host "  Swagger  : http://localhost:5000/swagger" -ForegroundColor Green
Write-Host "  Frontend : http://localhost:5173`n" -ForegroundColor Magenta

# Backend — yeni pencere, exe'den calistir.
# ASPNETCORE_ENVIRONMENT=Development sart: Production'da ApiKeyMiddleware anahtar yoksa
# tum /api'yi 503'ler ve CORS hicbir origin'e izin vermez (ALLOWED_ORIGINS bos).
# Calisma dizini publishDir olmali, yoksa appsettings.json cagiran cwd'den aranir.
Start-Process powershell -WorkingDirectory $publishDir -ArgumentList @(
    "-NoExit",
    "-Command",
    "`$env:ASPNETCORE_ENVIRONMENT='Development'; Write-Host '=== FitTrack Backend (:5000) ===' -ForegroundColor Green; Write-Host 'Baslatiliyor...' -ForegroundColor Gray; & '$publishDir\FitTrack.API.exe'"
)

# Frontend — yeni pencere
Start-Process powershell -ArgumentList @(
    "-NoExit",
    "-Command",
    "Write-Host '=== FitTrack Frontend (:5173) ===' -ForegroundColor Magenta; cd '$clientDir'; Write-Host 'Baslatiliyor...' -ForegroundColor Gray; npm run dev"
)

Write-Host "Uygulama aciliyor...`n" -ForegroundColor Cyan
Write-Host "Kapatmak icin acilan iki pencereyi de kapat." -ForegroundColor Gray
