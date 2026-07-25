# FitTrack

Kişisel sağlık takip uygulaması — beslenme, antrenman ve kilo takibi. Tek kullanıcı,
tek parola ile korunur (bkz. [Erişim koruması](#erişim-koruması)).

- **Backend** — `FitTrack.API` (ASP.NET Core 8, EF Core, SQLite)
- **Frontend** — `fittrack-client` (React + Vite + TypeScript + Tailwind, React Query, Recharts)

Özellikler: makro halkası + öğün takibi (yerel besin veritabanı, ~130 besin, API yok), set-bazlı antrenman kaydı
(ghost/önceki değerler), Recharts kilo grafiği, günlük hedefler, koyu/açık tema, mobil bottom-tab.

## Ekran görüntüleri

> _Placeholder — buraya ekran görüntüleri eklenecek._
>
> | Beslenme | Antrenman | Kilo | Ayarlar |
> |---|---|---|---|
> | _(görsel)_ | _(görsel)_ | _(görsel)_ | _(görsel)_ |

---

## Local kurulum

### Gereksinimler
.NET 8 SDK · Node 18+ · (yerel için veritabanı kurulumu gerekmez — SQLite)

### Backend

```bash
cd FitTrack/FitTrack.API
```

Veritabanı kurulumu yok. Uygulama her ortamda **SQLite** kullanır (`fittrack.db`) — ilk
açılışta şema oluşturulur (`EnsureCreated`) ve `UserGoals` varsayılanları seed edilir.
Dosya, `/var/data` dizini varsa oraya (kalıcı disk), yoksa uygulama binary'sinin yanına yazılır.

```bash
dotnet run
```

   API: `http://localhost:5000` · Swagger (dev): `http://localhost:5000/swagger`

### Frontend

```bash
cd FitTrack/fittrack-client
npm install
npm run dev
```

Client: `http://localhost:5173`. API adresi `.env` içindeki `VITE_API_URL` (varsayılan
`http://localhost:5000/api`).

---

## Erişim koruması

Uygulama internete açıldığında verinin önünde tek bir kapı vardır: `FITTRACK_API_KEY`.

- `/api/*` altındaki **her** istek `X-Api-Key` başlığı ister. Eşleşmezse `401`.
- Karşılaştırma sabit sürelidir (`CryptographicOperations.FixedTimeEquals`) — cevap
  gecikmesinden anahtar sızmaz.
- Anahtar sunucuda **tanımlı değilse**: `Development` ortamında kapı açık kalır (yerel
  geliştirme bozulmaz), `Production` ortamında API tamamen kapanır ve `503` döner.
  Yani yanlışlıkla korumasız yayına çıkmak mümkün değil.
- `/health` anahtar istemez (platform sağlık kontrolü için), veri döndürmez.
- Tarayıcı tarafında parola bir kez girilir, `localStorage`'da saklanır ve her isteğe
  eklenir. Herhangi bir istek `401` alırsa saklanan anahtar silinir ve kilit ekranı döner.
- Anthropic anahtarı frontend'e **hiç** çıkmaz — `CoachController` backend'de proxy'ler.

Anahtar üret:

```bash
openssl rand -base64 24
```

Telegram botu ayrı bir kapıdır: `Telegram:ChatId` verilirse sadece o sohbet kabul edilir.
Verilmezse **ilk yazan** sohbet sahiplenir ve bir daha değişmez — yabancı biri botu bulup
veriyi okuyamaz veya proaktif mesajları üstüne alamaz.

---

## Veritabanı ve yedekleme

### Veritabanı nerede?

Uygulama SQLite kullanır ve dosyayı **çalıştığı yerin yanına** yazar
(`AppContext.BaseDirectory`), `/var/data` varsa oraya. Bu yüzden nasıl çalıştırdığına
göre farklı dosyalar oluşur — hangisinin gerçek veri olduğunu karıştırmak kolaydır:

| Nasıl çalıştırdın | Veritabanı nerede |
|---|---|
| `dev.ps1` | `C:\ProgramData\FitTrack\fittrack.db` |
| `dotnet run` (IDE / terminal) | `FitTrack.API\bin\Debug\net8.0\fittrack.db` |
| Railway (canlı) | `/var/data/fittrack.db` (kalıcı volume) |

> **Canlı yayına geçtikten sonra gerçek veri buluttakidir.** Yerelde çalıştırırsan
> yerel dosyaya yazarsın ve veri ikiye bölünür.

Bir dosyanın içinde ne olduğunu görmek için:

```bash
node scripts/inspect-db.js backups\fittrack_2026-07-25_141436.db
```

### Yedek alma

```powershell
.\scripts\backup.ps1            # son 10 yedek saklanır
.\scripts\backup.ps1 -Keep 30
```

Canlı veritabanını `backups/` klasörüne tarih damgalı indirir, dosyanın gerçekten
geçerli bir SQLite veritabanı olduğunu doğrular (yarım inen dosyayı yedek saymaz) ve
eskileri budar. `backups/` git tarafından yok sayılır.

Önkoşul: `railway login`, `railway link` ve `railway ssh keys add` — volume dosya
erişimi SSH üzerinden çalışır.

### Yerel veriyi buluta taşıma

Canlı ortama geçerken yerel veritabanını yüklemek için:

```powershell
# 1. Calisan uygulama varken tutarli anlik goruntu al (duz kopya yarim islem yakalayabilir)
#    VACUUM INTO kullan — bkz. scripts/inspect-db.js ile dogrulama
# 2. Yukle
railway volume files --volume fittrack-volume upload <yerel.db> /var/data/fittrack.db --overwrite
# 3. Servisi yeniden baslat ki yeni dosyayi acsin
railway redeploy --yes
```

---

## Environment variables

### Backend (`FitTrack.API`)

| Değişken | Açıklama | Varsayılan |
|---|---|---|
| `FITTRACK_API_KEY` | API parolası. Üretimde **zorunlu** — yoksa API `503` ile kapalı kalır. | — |
| `ALLOWED_ORIGINS` | Virgülle ayrılmış izinli tarayıcı origin'leri (`https://fittrack.vercel.app`). Verilmezse: yerelde serbest, üretimde hiçbir origin. | — |
| `ANTHROPIC_API_KEY` | Koç için Anthropic anahtarı. Yerelde `dotnet user-secrets` (`Anthropic:ApiKey`) da olur. | — |
| `Telegram__BotToken` | Telegram bot token'ı. Verilmezse bot ve proaktif koç kapalı. | — |
| `Telegram__ChatId` | İzinli Telegram sohbeti. Verilmezse ilk yazan sohbet sahiplenir. | — |
| `PORT` | Uygulamanın bind olacağı port (`0.0.0.0:$PORT`). | `5000` |

> .NET'te iç içe config anahtarları ortam değişkeninde çift alt çizgi ile yazılır:
> `Telegram:BotToken` → `Telegram__BotToken`.

### Frontend (`fittrack-client`)

| Değişken | Açıklama | Varsayılan |
|---|---|---|
| `VITE_API_URL` | Backend API base URL'i (örn. `https://fittrack.up.railway.app/api`). | `http://127.0.0.1:5000/api` |

`.env` → local dev · `.env.production` → prod build (`npm run build`).
Parola `.env`'e **yazılmaz** — kullanıcı tarayıcıda girer.

---

## Deploy

Domain satın almak gerekmez — her iki platform da ücretsiz alt alan adı ve HTTPS verir.
Telefondan tarayıcıyla Vercel adresine girilir, "Ana Ekrana Ekle" ile uygulama gibi durur.

### 1. Backend → Railway

Railway seçilmesinin nedeni: SQLite dosyasının yaşaması için **kalıcı disk** ve Telegram
polling'i ile 14:00/21:00 proaktif mesajları için **uyumayan** bir servis gerekiyor.
Ücretsiz katmanda uyuyan bir platform (ör. Render free) her ikisini de bozar.

1. Repo'yu bağla, **Root Directory**: repo kökü (kökteki `Dockerfile` API'yi build eder).
2. **Volume** ekle, mount path: `/var/data`. `Program.cs` bu dizin varsa veritabanını
   oraya yazar; disk olmazsa veri her deploy'da sıfırlanır.
3. Variables:
   - `FITTRACK_API_KEY` = ürettiğin parola (**zorunlu**, yoksa API kapalı kalır)
   - `ANTHROPIC_API_KEY` = koç anahtarı
   - `Telegram__BotToken`, `Telegram__ChatId` (bot kullanılacaksa)
   - `PORT` Railway tarafından otomatik verilir, elle girme.
4. Deploy et, domain'i not al (`https://xxx.up.railway.app`).
5. `https://xxx.up.railway.app/health` → `{"status":"ok"}` dönmeli.

### 2. Frontend → Vercel

1. Repo'yu import et, **Root Directory**: `fittrack-client`.
2. Environment variable: `VITE_API_URL = https://xxx.up.railway.app/api`
3. Build `npm run build`, output `dist`. `vercel.json` SPA rewrite'ı hazır.
4. Deploy et, domain'i not al (`https://xxx.vercel.app`).

### 3. Kapıyı kapat

Railway'e dön, `ALLOWED_ORIGINS = https://xxx.vercel.app` ekle ve yeniden deploy et.
Bu adım atlanırsa tarayıcı istekleri CORS'a takılır.

Doğrulama — anahtarsız istek `401` dönmeli:

```bash
curl -i https://xxx.up.railway.app/api/goals
```

---

## API endpoints (aktif)

| Alan | Route |
|---|---|
| Beslenme | `POST /api/nutrition/log`, `GET /api/nutrition/today`, `GET /api/nutrition/summary?date=`, `DELETE /api/nutrition/log/{id}` (besin arama artık client-side, yerel DB) |
| Antrenman | `POST /api/workout/session`, `GET /api/workout/session/today`, `GET /api/workout/sessions?limit=`, `POST /api/workout/exercise`, `DELETE /api/workout/exercise/{id}`, `POST /api/workout/set`, `PUT /api/workout/set/{id}`, `DELETE /api/workout/set/{id}`, `GET /api/workout/exercise/{name}/history`, `GET /api/workout/exercises/list` |
| Kilo | `POST /api/weight/log`, `GET /api/weight/today`, `GET /api/weight/history?days=`, `GET /api/weight/logs?take=`, `GET /api/weight/stats`, `DELETE /api/weight/log/{id}` |
| Hedefler | `GET /api/goals`, `PUT /api/goals` |

> Not: eski CRUD controller'ları (`/api/mealentries`, `/api/workoutsessions`, `/api/weightlogs`)
> hâlâ mevcut ama frontend tarafından kullanılmıyor.

Varsayılan seed hedefler: **2500 kcal / 180g protein / 250g karb / 70g yağ**.
