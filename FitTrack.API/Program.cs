using System.Text.Json.Serialization;
using FitTrack.API.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddUserSecrets(typeof(Program).Assembly, optional: true);

const string CorsPolicy = "AllowAll";

// Render uses a persistent volume at /var/data for SQLite.
// Local dev uses a file next to the app binary.
var dbPath = Path.Combine(
    Directory.Exists("/var/data") ? "/var/data" : AppContext.BaseDirectory,
    "fittrack.db");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

// Tarayıcı erişimi sadece bilinen origin'lere. ALLOWED_ORIGINS virgülle ayrılır,
// örn. "https://fittrack.vercel.app". Verilmezse: yerelde serbest, üretimde kapalı.
var allowedOrigins = (Environment.GetEnvironmentVariable("ALLOWED_ORIGINS") ?? "")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy =>
    {
        policy.AllowAnyHeader().AllowAnyMethod();
        if (allowedOrigins.Length > 0) policy.WithOrigins(allowedOrigins);
        else if (builder.Environment.IsDevelopment()) policy.SetIsOriginAllowed(_ => true);
    });
});

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();
builder.Services.AddScoped<FitTrack.API.Services.CoachService>();
builder.Services.AddHostedService<FitTrack.API.Services.TelegramBotService>();
builder.Services.AddHostedService<FitTrack.API.Services.DailyCoachService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();
        db.Database.ExecuteSqlRaw("PRAGMA journal_mode=DELETE;");
        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "CheckIns" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_CheckIns" PRIMARY KEY,
                "Mood" INTEGER NOT NULL, "Energy" INTEGER NOT NULL, "Hunger" INTEGER NOT NULL,
                "Note" TEXT NULL, "Context" TEXT NULL, "LoggedAt" TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS "Profiles" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_Profiles" PRIMARY KEY,
                "HeightCm" REAL NULL, "TargetWeightKg" REAL NULL, "UpdatedAt" TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS "CoachMessages" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_CoachMessages" PRIMARY KEY,
                "Role" TEXT NOT NULL, "Content" TEXT NOT NULL, "CreatedAt" TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS "CoachNotes" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_CoachNotes" PRIMARY KEY,
                "Category" TEXT NOT NULL, "Content" TEXT NOT NULL, "CreatedAt" TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS "AppSettings" (
                "Key" TEXT NOT NULL CONSTRAINT "PK_AppSettings" PRIMARY KEY,
                "Value" TEXT NOT NULL
            );
            """);
        db.Database.ExecuteSqlRaw("DELETE FROM CoachMessages WHERE CreatedAt < datetime('now', '-3 days');");
    }
    catch (Exception ex) { app.Logger.LogError(ex, "DB init failed."); }
}

// Arayüz aynı imajdan servis edilir (wwwroot). Statik dosyalar korumasızdır —
// içlerinde veri yok, kilit ekranı zaten uygulamanın içinde.
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseCors(CorsPolicy);
app.UseMiddleware<FitTrack.API.Middleware.ApiKeyMiddleware>();
app.UseAuthorization();
app.MapControllers();

// Platform sağlık kontrolü — anahtar istemez, veri sızdırmaz.
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// SPA geri düşüşü: /api dışındaki her yol index.html döner ki istemci
// tarafı yönlendirme sayfa yenilendiğinde de çalışsın.
app.MapFallbackToFile("index.html");

var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
app.Run($"http://0.0.0.0:{port}");
