<#
.SYNOPSIS
    Railway'deki canli veritabanini yerel bir yedege indirir.

.DESCRIPTION
    Volume'daki fittrack.db dosyasini tarih damgali bir kopya olarak
    backups/ klasorune indirir, dosyanin gercekten gecerli bir SQLite
    veritabani oldugunu dogrular ve eski yedekleri budar.

    Onkosul: `railway login` yapilmis ve bu klasor `railway link` ile
    projeye baglanmis olmali. Volume dosya erisimi SSH uzerinden calisir,
    anahtar `railway ssh keys add` ile kayitli olmali.

.PARAMETER Keep
    Saklanacak yedek sayisi. Varsayilan 10.

.EXAMPLE
    .\scripts\backup.ps1
    .\scripts\backup.ps1 -Keep 30
#>
[CmdletBinding()]
param(
    [int]$Keep = 10
)

$ErrorActionPreference = "Stop"

$repoRoot  = Split-Path -Parent $PSScriptRoot
$backupDir = Join-Path $repoRoot "backups"
$volume    = "fittrack-volume"
$remote    = "/var/data/fittrack.db"

if (-not (Test-Path $backupDir)) {
    New-Item -ItemType Directory -Path $backupDir | Out-Null
}

Set-Location $repoRoot

$stamp  = Get-Date -Format "yyyy-MM-dd_HHmmss"
$target = Join-Path $backupDir "fittrack_$stamp.db"

Write-Host "Yedek indiriliyor -> $target" -ForegroundColor Gray
railway volume files --volume $volume download $remote $target

if ($LASTEXITCODE -ne 0 -or -not (Test-Path $target)) {
    Write-Host "HATA: indirme basarisiz." -ForegroundColor Red
    exit 1
}

# Gecerli bir SQLite dosyasi mi? Ilk 16 bayt "SQLite format 3\0" olmali —
# yarim inen ya da hata metni iceren bir dosyayi yedek sanmayalim.
$header = [System.Text.Encoding]::ASCII.GetString(
    [System.IO.File]::ReadAllBytes($target)[0..14]
)
if ($header -ne "SQLite format 3") {
    Write-Host "HATA: inen dosya SQLite veritabani degil. Yedek siliniyor." -ForegroundColor Red
    Remove-Item $target -Force
    exit 1
}

$sizeKb = [math]::Round((Get-Item $target).Length / 1KB, 1)
Write-Host "Yedek alindi: $([System.IO.Path]::GetFileName($target))  ($sizeKb KB)" -ForegroundColor Green

# Eski yedekleri buda
$all = Get-ChildItem $backupDir -Filter "fittrack_*.db" | Sort-Object LastWriteTime -Descending
if ($all.Count -gt $Keep) {
    $old = $all | Select-Object -Skip $Keep
    foreach ($f in $old) {
        Remove-Item $f.FullName -Force
        Write-Host "  eski yedek silindi: $($f.Name)" -ForegroundColor DarkGray
    }
}

Write-Host "Toplam yedek: $((Get-ChildItem $backupDir -Filter 'fittrack_*.db').Count) / $Keep" -ForegroundColor Gray
