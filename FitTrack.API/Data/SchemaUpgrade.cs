// EF1002: SQL'e yalnız SchemaUpgrade.OwnedTables sabit listesinden tablo adı giriyor; tüm değerler parametre.
#pragma warning disable EF1002
using FitTrack.API.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FitTrack.API.Data;

/// <summary>
/// Uygulama <c>EnsureCreated</c> kullanıyor; o, dolu bir veritabanına yeni tablo ya da kolon eklemez.
/// Tek kullanıcılı dönemden gelen canlı veritabanını çok kullanıcılı şemaya burada, idempotent olarak
/// taşıyoruz. Eski satırlar <see cref="AppDbContext.LegacyOwner"/> (boş Guid) ile işaretlenir; hiçbir
/// hesap onları göremez, ta ki eski parolayı bilen biri hesabına sahiplenene kadar.
/// </summary>
public static class SchemaUpgrade
{
    /// <summary>Kullanıcıya ait tüm tablolar. Hesap silme ve eski veri sahiplenme de bu listeyi kullanır.</summary>
    public static readonly string[] OwnedTables =
    {
        "MealEntries", "WorkoutSessions", "Exercises", "ExerciseSets", "WeightLogs",
        "UserGoals", "CheckIns", "Profiles", "CoachMessages", "CoachNotes",
    };

    public static void Run(AppDbContext db)
    {
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
            CREATE TABLE IF NOT EXISTS "Users" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_Users" PRIMARY KEY,
                "Email" TEXT NOT NULL,
                "DisplayName" TEXT NOT NULL,
                "PasswordHash" TEXT NOT NULL,
                "SessionVersion" INTEGER NOT NULL,
                "FailedLoginCount" INTEGER NOT NULL,
                "LockoutUntil" TEXT NULL,
                "TelegramChatId" INTEGER NULL,
                "TelegramLinkCodeHash" TEXT NULL,
                "TelegramLinkCodeExpiresAt" TEXT NULL,
                "CreatedAt" TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Users_Email" ON "Users" ("Email");
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Users_TelegramChatId" ON "Users" ("TelegramChatId") WHERE "TelegramChatId" IS NOT NULL;
            CREATE TABLE IF NOT EXISTS "UserApiKeys" (
                "UserId" TEXT NOT NULL CONSTRAINT "PK_UserApiKeys" PRIMARY KEY,
                "Ciphertext" TEXT NOT NULL,
                "Hint" TEXT NOT NULL,
                "CreatedAt" TEXT NOT NULL,
                "LastUsedAt" TEXT NULL
            );
            """);

        foreach (var table in OwnedTables)
        {
            if (!HasColumn(db, table, "UserId"))
            {
                // Tablo adı sabit listeden geliyor, kullanıcı girdisi değil.
                db.Database.ExecuteSqlRaw(
                    $"ALTER TABLE \"{table}\" ADD COLUMN \"UserId\" TEXT NOT NULL DEFAULT '{Guid.Empty}';");
            }
            db.Database.ExecuteSqlRaw(
                $"CREATE INDEX IF NOT EXISTS \"IX_{table}_UserId\" ON \"{table}\" (\"UserId\");");
        }
    }

    /// <summary>EF Core SQLite Guid'i büyük harfli TEXT olarak saklar. Ham SQL parametresi de öyle olmalı.</summary>
    public static string SqlGuid(Guid id) => id.ToString("D").ToUpperInvariant();

    public static bool HasLegacyData(AppDbContext db)
    {
        foreach (var table in OwnedTables)
        {
            var count = db.Database
                .SqlQueryRaw<long>($"SELECT COUNT(*) AS \"Value\" FROM \"{table}\" WHERE \"UserId\" = {{0}}", SqlGuid(Guid.Empty))
                .AsEnumerable().First();
            if (count > 0) return true;
        }
        return false;
    }

    private static bool HasColumn(AppDbContext db, string table, string column)
    {
        var conn = (SqliteConnection)db.Database.GetDbConnection();
        var wasOpen = conn.State == System.Data.ConnectionState.Open;
        if (!wasOpen) conn.Open();
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"PRAGMA table_info(\"{table}\");";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
        finally { if (!wasOpen) conn.Close(); }
    }
}
