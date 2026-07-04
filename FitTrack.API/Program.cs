using System.Text.Json.Serialization;
using FitTrack.API.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddUserSecrets(typeof(Program).Assembly, optional: true);

const string CorsPolicy = "AllowAll";

// Local dev: SQLite file. Deploy (Render/Neon): Postgres via DATABASE_URL env var.
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
var useSqlite = string.IsNullOrWhiteSpace(databaseUrl);
var sqlitePath = Path.Combine(AppContext.BaseDirectory, "fittrack.db");

builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (useSqlite)
        options.UseSqlite($"Data Source={sqlitePath}");
    else
        options.UseNpgsql(ConvertPostgresUrl(databaseUrl));
});

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();
builder.Services.AddHostedService<FitTrack.API.Services.TelegramBotService>();

var app = builder.Build();

// On startup: ensure all tables exist (both SQLite and Postgres paths work).
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();

        if (useSqlite)
        {
            db.Database.ExecuteSqlRaw("PRAGMA journal_mode=DELETE;");
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
            db.Database.ExecuteSqlRaw("DELETE FROM CoachMessages WHERE CreatedAt < datetime('now', '-3 days');");
        }
        else
        {
            // Postgres: idempotent CREATE TABLE IF NOT EXISTS + cleanup
            db.Database.ExecuteSqlRaw("""
                CREATE TABLE IF NOT EXISTS "CheckIns" (
                    "Id" uuid NOT NULL CONSTRAINT "PK_CheckIns" PRIMARY KEY,
                    "Mood" integer NOT NULL,
                    "Energy" integer NOT NULL,
                    "Hunger" integer NOT NULL,
                    "Note" text,
                    "Context" text,
                    "LoggedAt" timestamp with time zone NOT NULL
                );
                CREATE TABLE IF NOT EXISTS "Profiles" (
                    "Id" uuid NOT NULL CONSTRAINT "PK_Profiles" PRIMARY KEY,
                    "HeightCm" double precision,
                    "TargetWeightKg" double precision,
                    "UpdatedAt" timestamp with time zone NOT NULL
                );
                CREATE TABLE IF NOT EXISTS "CoachMessages" (
                    "Id" uuid NOT NULL CONSTRAINT "PK_CoachMessages" PRIMARY KEY,
                    "Role" text NOT NULL,
                    "Content" text NOT NULL,
                    "CreatedAt" timestamp with time zone NOT NULL
                );
                """);
            db.Database.ExecuteSqlRaw("DELETE FROM \"CoachMessages\" WHERE \"CreatedAt\" < NOW() - INTERVAL '3 days';");
        }
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

// Convert postgresql:// style URL to Npgsql keyword connection string.
static string ConvertPostgresUrl(string url)
{
    var uri = new Uri(url);
    var userInfo = uri.UserInfo.Split(':', 2);
    var db = uri.AbsolutePath.TrimStart('/');
    var portStr = uri.Port > 0 ? uri.Port : 5432;

    var cs = $"Host={uri.Host};Port={portStr};Database={db};Username={Uri.UnescapeDataString(userInfo[0])};Password={Uri.UnescapeDataString(userInfo.Length > 1 ? userInfo[1] : "")};SSL Mode=Require;Trust Server Certificate=true";

    // Append any query params like sslmode=require
    if (!string.IsNullOrEmpty(uri.Query))
    {
        var q = System.Web.HttpUtility.ParseQueryString(uri.Query);
        if (q["sslmode"] is "require" or "Require")
            cs = cs.Replace("SSL Mode=Require", "SSL Mode=Require");
    }

    return cs;
}
