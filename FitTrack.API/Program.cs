using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using FitTrack.API.Data;
using FitTrack.API.Models;
using FitTrack.API.Security;
using FitTrack.API.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddUserSecrets(typeof(Program).Assembly, optional: true);
var isDev = builder.Environment.IsDevelopment();

// Railway uses a persistent volume at /var/data for SQLite.
// Local dev uses a file next to the app binary. FITTRACK_DATA_DIR ikisini de ezer (testler kendi geçici klasörünü verir).
var dataDir = builder.Configuration["FITTRACK_DATA_DIR"] is { Length: > 0 } customDir ? customDir
    : Directory.Exists("/var/data") ? "/var/data" : AppContext.BaseDirectory;
var dbPath = Path.Combine(dataDir, "fittrack.db");

builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = 1024 * 1024); // 1 MB — API'nin en büyük isteği sohbet

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentUser>();
builder.Services.AddSingleton<KeyProtector>();
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

// Oturum çerezini imzalayan anahtarlar deploy'lar arasında kalıcı olmalı, yoksa her deploy herkesi dışarı atar.
builder.Services.AddDataProtection()
    .SetApplicationName("FitTrack")
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dataDir, "dataprotection-keys")));

// Railway TLS'i kendi kenarında sonlandırıp X-Forwarded-* ile iletir. ForwardLimit=1: yalnız en sağdaki
// (Railway'in eklediği) değer okunur, istemcinin kendi yazdığı sahte başlıklar hız sınırını atlatamaz.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.ForwardLimit = 1;
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        // __Host- öneki: yalnız HTTPS, alan adı ayarlanamaz, yol "/" — alt alan adından çerez enjeksiyonu kapanır.
        o.Cookie.Name = isDev ? "FitTrack.Session" : "__Host-FitTrack";
        o.Cookie.HttpOnly = true; // JavaScript okuyamaz → XSS oturumu çalamaz
        o.Cookie.SameSite = SameSiteMode.Strict; // başka siteden gelen istek çerez taşımaz → CSRF'ye ilk kalkan
        o.Cookie.SecurePolicy = isDev ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        o.Cookie.Path = "/";
        o.ExpireTimeSpan = TimeSpan.FromDays(14);
        o.SlidingExpiration = true;
        o.Events = new CookieAuthenticationEvents
        {
            OnValidatePrincipal = SessionValidation.ValidateAsync,
            // API yönlendirme değil durum kodu döner.
            OnRedirectToLogin = ctx => { ctx.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; },
            OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; },
        };
    });
builder.Services.AddAuthorization();

// Sınırlar yapılandırmadan okunabilir (RateLimit:Auth vb.) — varsayılanlar üretim değerleri.
var limits = builder.Configuration.GetSection("RateLimit");
int Limit(string name, int fallback) => limits.GetValue(name, fallback);

static string ClientIp(HttpContext ctx) => ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
static string UserOrIp(HttpContext ctx) => CurrentUser.FromPrincipal(ctx.User)?.ToString() ?? $"ip:{ClientIp(ctx)}";

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.ContentType = "application/json; charset=utf-8";
        await ctx.HttpContext.Response.WriteAsJsonAsync(new { error = "Çok fazla istek. Biraz bekleyip tekrar dene." }, ct);
    };

    // Giriş/kayıt: IP başına dakikada 10. Hesap başına kilit ayrıca AuthController'da.
    o.AddPolicy(RateLimits.Auth, ctx => RateLimitPartition.GetFixedWindowLimiter(ClientIp(ctx),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = Limit("Auth", 10), Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));

    // Koç: kullanıcı başına dakikada 20 — kendi anahtarı bile olsa sunucu kaynağı sınırlı.
    o.AddPolicy(RateLimits.Coach, ctx => RateLimitPartition.GetFixedWindowLimiter(UserOrIp(ctx),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = Limit("Coach", 20), Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));

    // Parola doğrulayan / anahtar sınayan uçlar: kullanıcı başına 10 dakikada 10.
    o.AddPolicy(RateLimits.Sensitive, ctx => RateLimitPartition.GetFixedWindowLimiter(UserOrIp(ctx),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = Limit("Sensitive", 10), Window = TimeSpan.FromMinutes(10), QueueLimit = 0 }));

    // Eski parola: tüm sunucu için saatte 20 deneme.
    o.AddPolicy(RateLimits.Legacy, _ => RateLimitPartition.GetFixedWindowLimiter("global",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = Limit("Legacy", 20), Window = TimeSpan.FromHours(1), QueueLimit = 0 }));
});

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddHttpClient();
builder.Services.AddScoped<ApiKeyService>();
builder.Services.AddScoped<CoachService>();
builder.Services.AddHostedService<TelegramBotService>();
builder.Services.AddHostedService<DailyCoachService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();
        db.Database.ExecuteSqlRaw("PRAGMA journal_mode=DELETE;");
        SchemaUpgrade.Run(db);
        db.Database.ExecuteSqlRaw("DELETE FROM CoachMessages WHERE CreatedAt < datetime('now', '-3 days');");
    }
    catch (Exception ex) { app.Logger.LogError(ex, "DB init failed."); }
}

// Açılışta hangi veritabanına ve hangi saate bağlı olduğumuzu yaz. Birden fazla
// örnek farklı dosyalara bakarken bu satır teşhisi saniyeler içinde bitiriyor.
app.Logger.LogInformation(
    "FitTrack açılıyor · Veritabanı: {Db} · Saat dilimi: {Tz} · Yerel saat: {Now:yyyy-MM-dd HH:mm}",
    dbPath, TimeZoneInfo.Local.Id, DateTime.Now);

app.UseForwardedHeaders();

// Beklenmeyen hata: yığın izi ya da iç ayrıntı istemciye gitmez.
// Sahiplik ihlali (AppDbContext.EnforceOwnership) 404 olarak döner — kaydın varlığı bile sızmasın.
app.UseExceptionHandler(errorApp => errorApp.Run(async ctx =>
{
    var ex = ctx.Features.Get<IExceptionHandlerFeature>()?.Error;
    var forbidden = ex is UnauthorizedAccessException;
    if (!forbidden) app.Logger.LogError(ex, "İşlenmeyen hata: {Path}", ctx.Request.Path);
    ctx.Response.StatusCode = forbidden ? StatusCodes.Status404NotFound : StatusCodes.Status500InternalServerError;
    ctx.Response.ContentType = "application/json; charset=utf-8";
    await ctx.Response.WriteAsJsonAsync(new { error = forbidden ? "Bulunamadı." : "Sunucu hatası." });
}));

app.UseMiddleware<SecurityHeadersMiddleware>();

// Arayüz aynı imajdan servis edilir (wwwroot). Statik dosyalar korumasızdır —
// içlerinde veri yok, giriş ekranı zaten uygulamanın içinde.
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseMiddleware<CsrfHeaderMiddleware>();
app.UseRateLimiter();
app.UseAuthorization();

// Varsayılan: her API ucu oturum ister. Açık olanlar [AllowAnonymous] ile tek tek işaretli.
app.MapControllers().RequireAuthorization();

// Platform sağlık kontrolü — oturum istemez, veri sızdırmaz.
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// Bilinmeyen /api yolu SPA'ya düşmesin, düz 404 dönsün.
app.Map("/api/{**rest}", () => Results.NotFound(new { error = "Bulunamadı." }));

// SPA geri düşüşü: /api dışındaki her yol index.html döner ki istemci
// tarafı yönlendirme sayfa yenilendiğinde de çalışsın.
app.MapFallbackToFile("index.html");

var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
app.Run($"http://0.0.0.0:{port}");

/// <summary>Entegrasyon testleri (WebApplicationFactory) için görünür giriş noktası.</summary>
public partial class Program { }
