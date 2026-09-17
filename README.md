# FitTrack

Kişisel sağlık takip uygulaması — beslenme, antrenman ve kilo takibi. Çok kullanıcılı:
herkes hesap açar, her hesabın verisi yalnız kendisine görünür. AI koçu, kullanıcının
kendi Anthropic anahtarıyla çalışır (bkz. [Hesaplar ve güvenlik](#hesaplar-ve-güvenlik)).

- **Tests** — `FitTrack.Tests` (xUnit, entegrasyon + birim, Docker'da koşar)

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
açılışta şema oluşturulur (`EnsureCreated`), eski veritabanı `SchemaUpgrade` ile çok kullanıcılı
şemaya taşınır.
Dosya, `/var/data` dizini varsa oraya (kalıcı disk), yoksa uygulama binary'sinin yanına yazılır.

```bash
dotnet run
```

   API: `http://localhost:5000`

### Frontend

```bash
cd FitTrack/fittrack-client
npm install
npm run dev
```

Client: `http://localhost:5173`. Vite `/api` isteklerini `127.0.0.1:5000`'e proxy'ler
(`vite.config.ts`) — oturum çerezi aynı origin ister, CORS kapalı.

---

## Hesaplar ve güvenlik

Ayrıntılı tehdit modeli, her kontrolün testi ve kapsam raporu:
[`docs/reports/2026-09-17-cok-kullanicili-gecis.md`](docs/reports/2026-09-17-cok-kullanicili-gecis.md).

**Kimlik.** E-posta + parola (PBKDF2, en az 10 karakter). Oturum `HttpOnly`, `SameSite=Strict`
çerezde; üretimde `__Host-` önekli ve `Secure`. Tarayıcıda hiçbir sır tutulmaz. Hesap başına
5 hatalı girişte 15 dk kilit, IP başına dakikada 10 giriş/kayıt. Parola değişimi ve
"her yerden çık" tüm diğer oturumları anında düşürür.

**Veri izolasyonu.** Kural controller'da değil `AppDbContext`'te: her kullanıcı tablosunda
global sorgu filtresi + `SaveChanges`'ta sahiplik kalkanı. Kimliksiz bağlam hiçbir satır görmez
ve yazamaz; başkasına ait satırı değiştirme girişimi istisna → `404`.

**AI anahtarı.** Kullanıcı Hesap → *AI anahtarı*'ndan ekler. Anthropic'e sorularak doğrulanır,
AES-256-GCM ile şifrelenir (kullanıcı ID'si şifrelemeye bağlı), yalnız `sk-ant-…abcd` ipucu
gösterilir. Ana anahtar `FITTRACK_ENCRYPTION_KEY` ortam değişkeninde — veritabanı ya da yedeği
sızsa anahtarlar okunamaz. Üretimde ana anahtar yoksa anahtar saklama tamamen kapanır.
Sunucuda paylaşılan Anthropic anahtarı **yok**: anahtarı olmayan koçu kullanamaz.

**CSRF.** `SameSite=Strict` + durum değiştiren her `/api` isteğinde `X-Requested-With: FitTrack`.

**Telegram.** Web'den 10 dk geçerli, tek kullanımlık kod alınır, bota `/link KOD` yazılır.
Bağlı olmayan ya da grup sohbeti hiçbir veriye erişemez. `/unlink` ya da web'den kaldırılır.

**Eski (tek kullanıcılı) veri.** Göçte sahipsiz işaretlenir, hiçbir hesap göremez. Hesap →
*Eski verileri sahiplen* ekranında eski `FITTRACK_API_KEY` parolasını giren hesaba taşınır.

Ana anahtar üret:

```powershell
[Convert]::ToBase64String((1..32 | % { [byte](Get-Random -Max 256) }))
```

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
| `FITTRACK_ENCRYPTION_KEY` | AI anahtarlarını şifreleyen 32 baytlık base64 ana anahtar. Üretimde yoksa anahtar saklama kapalı. Yerelde (Development) verilmezse `%LOCALAPPDATA%\FitTrack\dev-encryption.key` üretilir. | — |
| `FITTRACK_ENCRYPTION_KEY_PREVIOUS` | Ana anahtar döndürülürken eski anahtar. Eski kayıtlar okunur, ilk kullanımda yeniden şifrelenir. | — |
| `FITTRACK_API_KEY` | **Yalnız** eski tek kullanıcılı veriyi sahiplenmek için parola. Sahiplenme bitince silinebilir. | — |
| `Telegram__BotToken` | Telegram bot token'ı. Verilmezse bot ve proaktif koç kapalı. | — |
| `Telegram__Polling` | `true` olmadan bot yoklamaz (aynı token'ı iki örnek yoklamasın). | — |
| `RateLimit__Auth` / `__Coach` / `__Sensitive` / `__Legacy` | Hız sınırları (testler yükseltir). | 10 / 20 / 10 / 20 |
| `FITTRACK_DATA_DIR` | Veritabanı klasörünü ezer (testler kullanır). | `/var/data` ya da binary yanı |
| `PORT` | Uygulamanın bind olacağı port (`0.0.0.0:$PORT`). | `5000` |

> Artık okunmayanlar: `ANTHROPIC_API_KEY`, `ALLOWED_ORIGINS`, `Telegram__ChatId` (yalnız eski veri
> sahiplenilirken Telegram sohbetini taşımak için okunur).
>
> .NET'te iç içe config anahtarları ortam değişkeninde çift alt çizgi ile yazılır:
> `Telegram:BotToken` → `Telegram__BotToken`.

### Frontend (`fittrack-client`)

| Değişken | Açıklama | Varsayılan |
|---|---|---|
| `VITE_API_URL` | API base yolu. Arayüz ve API aynı origin'de olmalı. | `/api` |

Tarayıcıya hiçbir sır yazılmaz — oturum HttpOnly çerezde.

---

## Deploy

Tek servis: kökteki `Dockerfile` arayüzü derleyip API'nin `wwwroot`'una koyar. Arayüz ve API aynı
origin'den sunulur — oturum çerezi `SameSite=Strict` olduğu için **ayrı domainde frontend (Vercel)
artık çalışmaz**.

### Railway

1. Repo'yu bağla, **Root Directory**: repo kökü.
2. **Volume** ekle, mount path: `/var/data` (veritabanı + oturum çerezi imza anahtarları).
3. Variables:
   - `FITTRACK_ENCRYPTION_KEY` = ürettiğin ana anahtar (**parola yöneticisine de kaydet**)
   - `FITTRACK_API_KEY` = eski parola (yalnız eski veri sahiplenilene kadar)
   - `Telegram__BotToken`, `Telegram__Polling=true` (bot kullanılacaksa)
   - `PORT` Railway tarafından otomatik verilir.
4. Deploy et. `https://xxx.up.railway.app/health` → `{"status":"ok"}`.
5. Oturumsuz istek `401` dönmeli:
   ```bash
   curl -i https://xxx.up.railway.app/api/goals
   ```
6. Uygulamada kayıt ol → Hesap → *Eski verileri sahiplen* → *AI anahtarı*.

---

## Testler

```powershell
docker build -f Dockerfile.test -t fittrack-tests .
docker run --rm -v "${PWD}/docs/reports/coverage:/out" fittrack-tests
```

94 test: kimlik, kullanıcılar arası izolasyon, anahtar şifreleme ve kopyalama saldırısı, CSRF,
oturum iptali, hız sınırı, Telegram bağlama, gece koçu, eski şemadan göç. Gerçek `Program.cs` +
gerçek SQLite; Anthropic ve Telegram API'leri sahte (`FakeAnthropic`), ağa çıkılmaz.
Kapsam raporu `docs/reports/coverage/` altına yazılır.

Neden Docker: Windows'ta Smart App Control yeni derlenen DLL'leri engelliyor (GOTCHAS).

---

## API endpoints (aktif)

Oturum dışındaki her uç oturum ister; yazan her istek `X-Requested-With: FitTrack` başlığı ister.

| Alan | Route |
|---|---|
| Oturum | `POST /api/auth/register`, `POST /api/auth/login`, `POST /api/auth/logout`, `GET /api/auth/me` |
| Hesap | `GET/PUT/DELETE /api/account/ai-key`, `POST /api/account/password`, `POST /api/account/logout-all`, `POST /api/account/delete`, `GET/DELETE /api/account/telegram`, `POST /api/account/telegram/link-code`, `GET /api/account/legacy`, `POST /api/account/legacy/claim` |
| Koç | `POST /api/coach/chat` (kendi anahtarı yoksa `403 ai_key_missing`), `GET /api/coach/history` |
| Beslenme | `POST /api/nutrition/log`, `PUT/DELETE /api/nutrition/log/{id}`, `GET /api/nutrition/today`, `GET /api/nutrition/day/{date}`, `GET /api/nutrition/summary?date=`, `GET /api/nutrition/history?days=`, `GET /api/nutrition/streak` |
| Antrenman | `POST /api/workout/session`, `GET /api/workout/session/today`, `PUT/DELETE /api/workout/session/{id}`, `GET /api/workout/sessions?limit=`, `POST /api/workout/exercise`, `PUT/DELETE /api/workout/exercise/{id}`, `POST /api/workout/set`, `PUT/DELETE /api/workout/set/{id}`, `GET /api/workout/exercise/{name}/history`, `GET /api/workout/exercises/list` |
| Kilo | `POST /api/weight/log`, `GET /api/weight/today`, `GET /api/weight/history?days=`, `GET /api/weight/logs?take=`, `GET /api/weight/stats`, `PUT/DELETE /api/weight/log/{id}` |
| Durum | `POST /api/checkin`, `GET /api/checkin/today`, `GET /api/checkin/recent`, `DELETE /api/checkin/{id}` |
| Hedef/Profil | `GET/PUT /api/goals`, `GET/PUT /api/profile` |

Eski genel CRUD controller'ları (`/api/mealentries`, `/api/workoutsessions`, `/api/weightlogs`)
**kaldırıldı** — kullanılmıyorlardı ve tüm alanları istemciden kabul ediyorlardı.

Yeni hesabın varsayılan hedefleri: **2500 kcal / 180g protein / 250g karb / 70g yağ**.

---

## GOTCHAS

### Girişten sonra ekran giriş formunda takılı kalıyor — `queryClient.clear()` oturum sorgusunu da siliyor (2026-09-17, TanStack Query 5)

**Semptom.** Kayıt/giriş sunucuda başarılı (kullanıcı veritabanında, çerez yazılmış), ama ekran
değişmiyor, hata da yok. Sayfa yenilenince uygulama açılıyor.

**Neden.** Oturum değişince önceki kullanıcının önbelleği atılsın diye `qc.clear()` +
`qc.setQueryData(["session"], yeni)` yapılıyordu. `clear()` sorguyu önbellekten **kaldırır**;
`useSession`'ın gözlemcisi kaldırılmış eski sorgu nesnesine bağlı kalır. `setQueryData` yeni bir
sorgu nesnesi yaratır, gözlemci onu hiç görmez.

**Çözüm.** Önce oturumu yaz, sonra oturum **dışındaki** sorguları kaldır
(`hooks/useSession.ts → useResetSession`). Headless UI testi yakaladı; birim testle görünmezdi.

### Testler Windows'ta rastgele `FileLoadException: Uygulama Denetimi ilkesi bu dosyayı engelledi` (2026-09-17, .NET 8)

**Neden.** Smart App Control yeni derlenen DLL'leri bulut itibar sorgusuyla engelliyor.
Self-signed imza çözmüyor; Microsoft imzalı DLL'leri yeniden imzalamak onları da bozuyor
(`Microsoft.EntityFrameworkCore.dll` engellendi).

**Çözüm.** Testler ve çalıştırma Linux container'da: `Dockerfile.test` (testler + kapsam),
kökteki `Dockerfile` (uygulama).


### `dev.ps1` backend'i Production ortamında başlatıyordu — tüm `/api` 503, CORS reddi (2026-08-20, .NET 8 / ASP.NET Core 8)

**Semptom:** `dev.ps1` sorunsuz tamamlanıyor, iki pencere açılıyor, 5000 ve 5173 portları
dinlemede. Ama arayüz "bağlanılamıyor" diyor. Backend log'unda:

```
fail: FitTrack.API.Middleware.ApiKeyMiddleware[0]
      FITTRACK_API_KEY yok - API tamamen kapalı.
info: ...CorsService[6] Request origin http://localhost:5173 does not have permission
GET /api/goals - 503
Hosting environment: Production
Content root path: C:\Users\ozane\Documents\OzanOS   <-- dev.ps1'in çağrıldığı dizin
```

**Neden:** `dotnet publish -c Debug` **ortamı** Development yapmaz — `-c Debug` sadece
derleme yapılandırmasıdır. `ASPNETCORE_ENVIRONMENT` ayarlı değilse ASP.NET Core
**Production**'a düşer. Production'da iki koruma birden kapıyı kapatır:

- `ApiKeyMiddleware`: anahtar yoksa yerelde geçirir, **Production'da 503'ler** (yanlışlıkla
  korumasız yayına çıkmayı imkânsız kılmak için bilinçli tasarım).
- `Program.cs` CORS: `ALLOWED_ORIGINS` boşsa "her origin"e izin yalnızca
  `IsDevelopment()` dalında verilir; Production'da hiçbir origin geçmez.

Ayrıca `Start-Process` çalışma dizinini miras aldığı için content root, `dev.ps1`'in
çağrıldığı klasör oluyordu — `appsettings.json` yanlış yerde aranıyor. (DB etkilenmiyor;
o `AppContext.BaseDirectory` üzerinden çözülüyor.)

**Çözüm:** `dev.ps1` içindeki backend `Start-Process` çağrısına iki şey eklendi:

```powershell
Start-Process powershell -WorkingDirectory $publishDir -ArgumentList @(
    "-NoExit", "-Command",
    "`$env:ASPNETCORE_ENVIRONMENT='Development'; ... & '$publishDir\FitTrack.API.exe'"
)
```

**Doğrulama:** `GET http://127.0.0.1:5000/api/goals` (`Origin: http://localhost:5173`
başlığıyla) → `200` + `Access-Control-Allow-Origin: http://localhost:5173`.

**Aynı kökten üçüncü semptom — ölü AI koç:** `Anthropic:ApiKey` ve `Telegram:BotToken`
bu makinede **.NET User Secrets**'ta duruyor (`UserSecretsId` = `03d3557b-...`, dosya
`%APPDATA%\Microsoft\UserSecrets\<id>\secrets.json`). User Secrets sağlayıcısı config
zincirine **yalnızca Development'ta** eklenir. Production'a düşünce `secrets.json` hiç
okunmadı → `CoachService.ApiKey` boş → `ChatAsync` sessizce boş cevap döndürdü
(exception yok, log yok — teşhisi zorlaştıran kısım bu).

Anahtarı ortam değişkenine taşımaya **gerek yok**; ortam Development olduğu sürece
secrets okunur. Doğrulama:

```powershell
Invoke-WebRequest -Uri http://127.0.0.1:5000/api/coach/chat -Method POST `
  -ContentType 'application/json; charset=utf-8' `
  -Body '{"messages":[{"role":"user","content":"test"}]}'
# -> 200 {"reply":"...","actions":[]}
```

Not: gövde alanı `messages` (dizi), `message` değil — yanlışı `400 {"error":"Boş mesaj."}` döner.

### Siyah ekran: `Ok(null)` → 204 → axios `data: ""` → `<WorkoutCard>` çöküyor (2026-08-20, ASP.NET Core 8 / axios 1.x)

**Semptom:** `localhost:5173` açılıyor, sunucu `200` dönüyor, index.html geliyor —
ama sayfa **tamamen siyah**. Boş sayfa değil: `<html class="dark">` uygulanmış, `#root`
boş. Konsolda:

```
Uncaught TypeError: Cannot read properties of undefined (reading 'reduce')
  source: /src/App.tsx
  An error occurred in the <WorkoutCard> component.
```

**Neden — üç parça üst üste:**

1. ASP.NET Core'da `return Ok(session)` — `session` null ise **204 No Content**, gövdesiz.
   (`WorkoutController.GetToday`, `WeightController` `today` ucu da aynı.)
2. axios gövdesiz cevabı `response.data = ""` yapar — `null` değil, **boş string**.
   Client'ta tip `WorkoutSession | null` yazıyor olsa bile runtime değeri `""`.
3. `current?.exercises.reduce(...)` — optional chain **boş stringde tökezlemez**
   (`""` null/undefined değil), `"".exercises` → `undefined`, `.reduce` → TypeError.

Ve React'te error boundary olmadığı için tek bileşenin hatası **tüm ağacı** söküyor —
"kart boş görünür" yerine "her şey siyah".

**En sinsi tarafı:** hata yalnızca **o gün henüz antrenman kaydı yokken** çıkıyor.
Bir set girildiği anda uc 200 + JSON dönüyor ve uygulama düzeliyor — yani sabah
açınca bozuk, akşam açınca sağlam. Varsayılan durum test edilmezse görülmez.

**Çözüm:** `src/api/axios.ts` içinde tek bir response interceptor — sözleşme
`T | null` diyorsa runtime da null vermeli:

```ts
api.interceptors.response.use((response) => {
  if (response.status === 204 || response.data === "") response.data = null;
  return response;
});
```

Ayrıca `App.tsx` `WorkoutCard` içinde `current?.exercises?.reduce(...)` (ikinci `?.`).

**Doğrulama:** headless chromium ile render + konsol yakalama — `Uncaught` satırı yok,
`#root` dolu, kartlar veriyle geliyor:

```bash
"$LOCALAPPDATA/ms-playwright/chromium-1234/chrome-win64/chrome.exe" \
  --headless=new --disable-gpu --virtual-time-budget=9000 --enable-logging=stderr \
  --screenshot=out.png --dump-dom http://localhost:5173/ > dom.html 2> console.txt
```

**Kapatıldı (aynı gün):** `src/components/ErrorBoundary.tsx` eklendi — altı özet kartı,
altı bölüm overlay’i ve kökteki `AuthGate` ayrı ayrı sarıldı. Bir kart çökerse yalnızca
o kart “‹ad› açılamadı” + “Tekrar dene” fallback’ine dönüyor, geri kalan ekran çalışıyor.

Ayrıca iki uç (`WorkoutController.GetToday`, `WeightController.Today`) `new JsonResult(...)`
ile artık 204 yerine **200 + `null`** dönüyor — sözleşme client interceptor’ına bağımlı
olmaktan çıktı. `Ok(x)` deseni null dönebilen başka uçta kalmadı (tarandı).

Sınırın çalıştığı kasten hata fırlatılarak doğrulandı: tek `role="alert"`, diğer kartlar sağlam.

### `--headless=new` `--window-size`’ı viewport’a uygulamaz — mobil testi sessizce yalan söyler (2026-08-20, Chromium 1234)

**Semptom:** Arayüz 375px’te ekran dışına taşıyor göründü: kartlar sağdan kesik,
alttaki 5 sekmeden 4’ü sığmış, `sm:` breakpoint’i mobilde aktif gibi davranıyordu.
Gerçekte layout’ta hiçbir sorun yoktu.

**Neden:** `chrome.exe --headless=new` `--window-size` bayrağını **CSS viewport’una
uygulamıyor** — kendi varsayılan genişliğinde render edip ekran görüntüsünü istenen
boyuta kırpıyor. Ölçüldü:

```bash
# probe.html:  <b id="o"></b><script>o.textContent='VIEWPORT='+innerWidth</script>
chrome.exe --headless=new  --window-size=375,800 --dump-dom probe.html   # VIEWPORT=504  ✗
chrome-headless-shell.exe  --window-size=375,800 --dump-dom probe.html   # VIEWPORT=375  ✓
```

504px ≥ 640px değil ama kartların hesaplandığı genişlik istenen 375 değildi; görüntü
375’e kırpılınca sağ taraf kesik geçti ve “layout bozuk” gibi okundu.

**Çözüm:** duyarlılık testinde **`chrome-headless-shell.exe`** kullan, `--headless=new`
değil. Masaüstü tek genişlik renderı için ikisi de olur; genişliğin kendisi ölçülen
şeyse shell şart.

```bash
SHELL_BIN="$LOCALAPPDATA/ms-playwright/chromium_headless_shell-1234/chrome-headless-shell-win64/chrome-headless-shell.exe"
for W in 320 375 414 768; do
  "$SHELL_BIN" --disable-gpu --hide-scrollbars --virtual-time-budget=8000 \
    --window-size=$W,900 --screenshot=w$W.png http://localhost:5173/
done
```

**Ders:** ölçen aracın kendisi doğrulanmadan bulgusuna güvenilmez. Burada araç hata
vermedi, sessizce yanlış genişlikte ölçtü — var olmayan bir hatayı kovalattı.
