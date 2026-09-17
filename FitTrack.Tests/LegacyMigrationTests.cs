using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FitTrack.Tests.Infrastructure;
using Microsoft.Data.Sqlite;

namespace FitTrack.Tests;

/// <summary>
/// Canlıdaki tek kullanıcılı veritabanının birebir şemasıyla başlar (UserId kolonu yok, veri dolu).
/// Göçün veriyi kaybetmediği, kimseye göstermediği ve yalnız eski parolayla sahiplenilebildiği test edilir.
/// </summary>
public class LegacyApp : TestApp
{
    public static readonly Guid LegacyGoalsId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    protected override void BeforeStart()
    {
        using var conn = new SqliteConnection($"Data Source={DbPath};Pooling=False");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE "CheckIns" ("Id" TEXT NOT NULL CONSTRAINT "PK_CheckIns" PRIMARY KEY, "Mood" INTEGER NOT NULL, "Energy" INTEGER NOT NULL, "Hunger" INTEGER NOT NULL, "Note" TEXT NULL, "Context" TEXT NULL, "LoggedAt" TEXT NOT NULL);
            CREATE TABLE "CoachMessages" ("Id" TEXT NOT NULL CONSTRAINT "PK_CoachMessages" PRIMARY KEY, "Role" TEXT NOT NULL, "Content" TEXT NOT NULL, "CreatedAt" TEXT NOT NULL);
            CREATE TABLE "MealEntries" ("Id" TEXT NOT NULL CONSTRAINT "PK_MealEntries" PRIMARY KEY, "FoodName" TEXT NOT NULL, "Grams" REAL NOT NULL, "Calories" REAL NOT NULL, "Protein" REAL NOT NULL, "Carbs" REAL NOT NULL, "Fat" REAL NOT NULL, "MealType" INTEGER NOT NULL, "LoggedAt" TEXT NOT NULL);
            CREATE TABLE "Profiles" ("Id" TEXT NOT NULL CONSTRAINT "PK_Profiles" PRIMARY KEY, "HeightCm" REAL NULL, "TargetWeightKg" REAL NULL, "UpdatedAt" TEXT NOT NULL);
            CREATE TABLE "UserGoals" ("Id" TEXT NOT NULL CONSTRAINT "PK_UserGoals" PRIMARY KEY, "CalorieGoal" REAL NOT NULL, "ProteinGoal" REAL NOT NULL, "CarbGoal" REAL NOT NULL, "FatGoal" REAL NOT NULL, "UpdatedAt" TEXT NOT NULL);
            CREATE TABLE "WeightLogs" ("Id" TEXT NOT NULL CONSTRAINT "PK_WeightLogs" PRIMARY KEY, "WeightKg" REAL NOT NULL, "LoggedAt" TEXT NOT NULL, "Notes" TEXT NULL);
            CREATE TABLE "WorkoutSessions" ("Id" TEXT NOT NULL CONSTRAINT "PK_WorkoutSessions" PRIMARY KEY, "Name" TEXT NOT NULL, "LoggedAt" TEXT NOT NULL);
            CREATE TABLE "Exercises" ("Id" TEXT NOT NULL CONSTRAINT "PK_Exercises" PRIMARY KEY, "WorkoutSessionId" TEXT NOT NULL, "Name" TEXT NOT NULL, "MuscleGroup" TEXT NOT NULL, CONSTRAINT "FK_Exercises_WorkoutSessions_WorkoutSessionId" FOREIGN KEY ("WorkoutSessionId") REFERENCES "WorkoutSessions" ("Id") ON DELETE CASCADE);
            CREATE TABLE "ExerciseSets" ("Id" TEXT NOT NULL CONSTRAINT "PK_ExerciseSets" PRIMARY KEY, "ExerciseId" TEXT NOT NULL, "SetNumber" INTEGER NOT NULL, "WeightKg" REAL NOT NULL, "Reps" INTEGER NOT NULL, "IsCompleted" INTEGER NOT NULL, CONSTRAINT "FK_ExerciseSets_Exercises_ExerciseId" FOREIGN KEY ("ExerciseId") REFERENCES "Exercises" ("Id") ON DELETE CASCADE);
            CREATE INDEX "IX_Exercises_WorkoutSessionId" ON "Exercises" ("WorkoutSessionId");
            CREATE INDEX "IX_ExerciseSets_ExerciseId" ON "ExerciseSets" ("ExerciseId");
            CREATE TABLE "CoachNotes" ("Id" TEXT NOT NULL CONSTRAINT "PK_CoachNotes" PRIMARY KEY, "Category" TEXT NOT NULL, "Content" TEXT NOT NULL, "CreatedAt" TEXT NOT NULL);
            CREATE TABLE "AppSettings" ("Key" TEXT NOT NULL CONSTRAINT "PK_AppSettings" PRIMARY KEY, "Value" TEXT NOT NULL);

            INSERT INTO MealEntries VALUES ('AAAAAAAA-0000-0000-0000-000000000001','Eski-mercimek',300,390,21,60,7,1,strftime('%Y-%m-%d %H:%M:%S','now','localtime'));
            INSERT INTO WeightLogs VALUES ('AAAAAAAA-0000-0000-0000-000000000002',93.4,strftime('%Y-%m-%d 00:00:00','now','localtime'),'eski');
            INSERT INTO UserGoals VALUES ('11111111-1111-1111-1111-111111111111',2222,180,250,70,'2026-01-01 00:00:00');
            INSERT INTO Profiles VALUES ('22222222-2222-2222-2222-222222222222',181,80,'2026-01-01 00:00:00');
            INSERT INTO WorkoutSessions VALUES ('AAAAAAAA-0000-0000-0000-000000000003','Eski-bacak',strftime('%Y-%m-%d %H:%M:%S','now','localtime'));
            INSERT INTO Exercises VALUES ('AAAAAAAA-0000-0000-0000-000000000004','AAAAAAAA-0000-0000-0000-000000000003','Squat','Bacak');
            INSERT INTO ExerciseSets VALUES ('AAAAAAAA-0000-0000-0000-000000000005','AAAAAAAA-0000-0000-0000-000000000004',1,100,5,1);
            INSERT INTO CheckIns VALUES ('AAAAAAAA-0000-0000-0000-000000000006',4,4,3,'eski-not','general',strftime('%Y-%m-%d %H:%M:%S','now','localtime'));
            INSERT INTO CoachNotes VALUES ('AAAAAAAA-0000-0000-0000-000000000007','injury','Eski-omuz','2026-01-01 00:00:00');
            INSERT INTO AppSettings VALUES ('telegram.chatId','424242');
            """;
        cmd.ExecuteNonQuery();
    }
}

public class LegacyMigrationTests : IClassFixture<LegacyApp>
{
    private readonly LegacyApp _app;
    public LegacyMigrationTests(LegacyApp app) => _app = app;

    [Fact]
    public async Task Migration_claim_flow()
    {
        // Göç: tüm tablolara UserId eklendi, satırlar sahipsiz, veri kaybı yok.
        var (first, firstId, _, _) = await _app.RegisterAsync("Ilk");
        foreach (var table in new[] { "MealEntries", "WeightLogs", "UserGoals", "Profiles", "WorkoutSessions", "Exercises", "ExerciseSets", "CheckIns", "CoachNotes" })
            Assert.Equal(1, _app.Scalar($"SELECT COUNT(*) FROM \"{table}\" WHERE UserId = '00000000-0000-0000-0000-000000000000'"));

        // İlk kayıt olan kişi bile eski veriyi görmez.
        Assert.DoesNotContain("Eski-mercimek", await first.GetStringAsync("/api/nutrition/today"));
        Assert.Equal(JsonValueKind.Null, (await (await first.GetAsync("/api/weight/stats")).JsonAsync()).GetProperty("currentWeight").ValueKind);
        Assert.Equal(2500, (await (await first.GetAsync("/api/goals")).JsonAsync()).GetProperty("calorieGoal").GetDouble());
        Assert.Equal("[]", await first.GetStringAsync("/api/workout/sessions"));

        Assert.True((await (await first.GetAsync("/api/account/legacy")).JsonAsync()).GetProperty("available").GetBoolean());

        // Yanlış parola: hiçbir şey taşınmaz.
        Assert.Equal(HttpStatusCode.BadRequest, (await first.PostAsJsonAsync("/api/account/legacy/claim", new { legacyPassword = "tahmin" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await first.PostAsJsonAsync("/api/account/legacy/claim", new { legacyPassword = "" })).StatusCode);
        Assert.Equal(1, _app.Scalar("SELECT COUNT(*) FROM MealEntries WHERE UserId = '00000000-0000-0000-0000-000000000000'"));

        // Doğru parolayı bilen (eski sahip) sahiplenir.
        var (owner, ownerId, _, _) = await _app.RegisterAsync("Sahip");
        await owner.GetAsync("/api/goals"); // hesap kendi boş hedefini açmış olsun
        Assert.Equal(HttpStatusCode.NoContent, (await owner.PostAsJsonAsync("/api/account/legacy/claim", new { legacyPassword = TestApp.LegacyPassword })).StatusCode);

        Assert.Contains("Eski-mercimek", await owner.GetStringAsync("/api/nutrition/today"));
        Assert.Equal(93.4, (await (await owner.GetAsync("/api/weight/stats")).JsonAsync()).GetProperty("currentWeight").GetDouble());
        var goals = await (await owner.GetAsync("/api/goals")).JsonAsync();
        Assert.Equal(2222, goals.GetProperty("calorieGoal").GetDouble()); // eski hedef, yeni boş kaydın yerini aldı
        Assert.Equal(1, _app.Scalar("SELECT COUNT(*) FROM UserGoals WHERE UserId=$u", ("$u", TestApp.SqlGuid(ownerId))));
        Assert.Contains("Squat", await owner.GetStringAsync("/api/workout/session/today"));
        Assert.Contains("eski-not", await owner.GetStringAsync("/api/checkin/recent"));

        // Eski Telegram sahibi de bu hesaba bağlandı.
        Assert.Equal(424242, _app.Scalar("SELECT TelegramChatId FROM Users WHERE Id=$u", ("$u", TestApp.SqlGuid(ownerId))));

        // Başkası hâlâ göremez; sahiplenme kapandı.
        Assert.DoesNotContain("Eski-mercimek", await first.GetStringAsync("/api/nutrition/today"));
        Assert.False((await (await first.GetAsync("/api/account/legacy")).JsonAsync()).GetProperty("available").GetBoolean());
        Assert.Equal(HttpStatusCode.NotFound, (await first.PostAsJsonAsync("/api/account/legacy/claim", new { legacyPassword = TestApp.LegacyPassword })).StatusCode);
        Assert.Equal(0, _app.Scalar("SELECT COUNT(*) FROM MealEntries WHERE UserId = '00000000-0000-0000-0000-000000000000'"));
        _ = firstId;
    }
}
