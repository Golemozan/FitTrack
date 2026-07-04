# FitTrack

Kişisel sağlık takip uygulaması — beslenme, antrenman ve kilo takibi. Tek kullanıcı, auth yok.

- **Backend** — `FitTrack.API` (ASP.NET Core 8, EF Core, PostgreSQL)
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

Yerelde kurulum yok. `DATABASE_URL` **verilmezse** uygulama otomatik olarak bir **SQLite**
dosyası (`fittrack.db`) kullanır — sunucu/port yok, ilk açılışta şema oluşturulur ve
`UserGoals` varsayılanları seed edilir. `DATABASE_URL` verilirse (deploy) Postgres kullanılır.

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

## Environment variables

### Backend (`FitTrack.API`)

| Değişken | Açıklama | Varsayılan |
|---|---|---|
| `DATABASE_URL` | Postgres URI (`postgresql://user:pass@host:port/db`). Verilirse Npgsql formatına çevrilip kullanılır; verilmezse `appsettings.json` connection string'i. SSL zorunlu kılınır. | — |
| `PORT` | Uygulamanın bind olacağı port (`0.0.0.0:$PORT`). | `5000` |

### Frontend (`fittrack-client`)

| Değişken | Açıklama | Varsayılan |
|---|---|---|
| `VITE_API_URL` | Backend API base URL'i (örn. `https://app.railway.app/api`). | `http://localhost:5000/api` |

`.env` → local dev · `.env.production` → prod build (`npm run build`).

---

## Deploy

### Backend → Railway

1. Yeni proje → **PostgreSQL** plugin ekle (Railway `DATABASE_URL` sağlar).
2. Repo'yu bağla, root: `FitTrack/FitTrack.API` (Nixpacks .NET'i otomatik build eder).
3. Variables: gerekli değil (besin verisi yerel). `PORT` Railway tarafından otomatik verilir.
4. Deploy. Başlangıçta migration otomatik çalışır. Domain'i not al (`https://xxx.railway.app`).

`Program.cs` içindeki `app.Run("http://0.0.0.0:" + PORT)` bind'i ve `DATABASE_URL` → Npgsql
dönüşümü Railway ile uyumludur. CORS `AllowAll` olduğu için Vercel origin'i sorunsuz erişir.

### Frontend → Vercel

1. Repo'yu import et, **Root Directory**: `FitTrack/fittrack-client`.
2. Environment variable: `VITE_API_URL = https://<railway-domain>/api`
   (veya `.env.production` dosyasını güncelle).
3. Build `npm run build`, output `dist`. `vercel.json` SPA rewrite'ı client-side routing için hazır.
4. Deploy.

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
