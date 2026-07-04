using System.Text.Json.Serialization;
using FitTrack.API.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Load user-secrets in any environment (local runs default to Production without a
// launch profile). Secrets hold the Anthropic API key — never commit it to appsettings.
builder.Configuration.AddUserSecrets(typeof(Program).Assembly, optional: true);

const string CorsPolicy = "AllowAll";

// Local dev needs zero setup: with no DATABASE_URL we use a SQLite file (no server,
// no port, instant). Deploys set DATABASE_URL → Postgres (Railway).
// Absolute path prevents creating a new empty DB when launched from a different CWD.
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
var useSqlite = string.IsNullOrWhiteSpace(databaseUrl);
var sqlitePath = Path.Combine(AppContext.BaseDirectory, "fittrack.db");
builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (useSqlite)
        options.UseSqlite($"Data Source={sqlitePath}");
    else
        options.UseNpgsql(BuildConnectionString(builder.Configuration));
});

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        // Serialize enums (MealType) as strings.
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Outbound HTTP (Anthropic coach proxy).
builder.Services.AddHttpClient();

// Telegram polling bot (starts automatically with the app).
builder.Services.AddHostedService<FitTrack.API.Services.TelegramBotService>();

var app = builder.Build();

// Apply pending migrations automatically on startup (single-user personal app).
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (useSqlite)
        {
            db.Database.EnsureCreated();
            // Disable WAL — with WAL, hard-killed process loses uncheckpointed data.
            db.Database.ExecuteSqlRaw("PRAGMA journal_mode=DELETE;");
            // EnsureCreated does nothing if the DB already exists, so newer tables
            // added after the first run won't appear. Create them additively here.
            db.Database.ExecuteSqlRaw("""
                CREATE TABLE IF NOT EXISTS "CheckIns" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_CheckIns" PRIMARY KEY,
                    "Mood" INTEGER NOT NULL,
                    "Energy" INTEGER NOT NULL,
                    "Hunger" INTEGER NOT NULL,
                    "Note" TEXT NULL,
                    "Context" TEXT NULL,
                    "LoggedAt" TEXT NOT NULL
                );
                CREATE TABLE IF NOT EXISTS "Profiles" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_Profiles" PRIMARY KEY,
                    "HeightCm" REAL NULL,
                    "TargetWeightKg" REAL NULL,
                    "UpdatedAt" TEXT NOT NULL
                );
                CREATE TABLE IF NOT EXISTS "CoachMessages" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_CoachMessages" PRIMARY KEY,
                    "Role" TEXT NOT NULL,
                    "Content" TEXT NOT NULL,
                    "CreatedAt" TEXT NOT NULL
                );
                """);

            // Clean coach history older than 3 days.
            db.Database.ExecuteSqlRaw("DELETE FROM CoachMessages WHERE CreatedAt < datetime('now', '-3 days');");
        }
        else
            db.Database.Migrate();
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Database init failed on startup.");
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(CorsPolicy);
app.UseAuthorization();
app.MapControllers();

var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
app.Run($"http://0.0.0.0:{port}");

static string BuildConnectionString(IConfiguration config)
{
    var url = Environment.GetEnvironmentVariable("DATABASE_URL");
    if (string.IsNullOrWhiteSpace(url))
        return config.GetConnectionString("DefaultConnection") ?? string.Empty;

    var uri = new Uri(url);
    var userInfo = uri.UserInfo.Split(':', 2);
    var db = uri.AbsolutePath.TrimStart('/');
    var port = uri.Port > 0 ? uri.Port : 5432;

    return $"Host={uri.Host};Port={port};Database={db};" +
           $"Username={Uri.UnescapeDataString(userInfo[0])};" +
           $"Password={Uri.UnescapeDataString(userInfo.Length > 1 ? userInfo[1] : string.Empty)};" +
           "SSL Mode=Require;Trust Server Certificate=true";
}
