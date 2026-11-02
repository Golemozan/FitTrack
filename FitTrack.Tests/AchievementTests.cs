using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FitTrack.Tests.Infrastructure;

namespace FitTrack.Tests;

/// <summary>Başarımlar: veriden hesaplanır, kademe bir kez açılır, ilk değerlendirme sessizdir (2026-11-01).</summary>
public class AchievementTests : IClassFixture<TestApp>
{
    private readonly TestApp _app;
    public AchievementTests(TestApp app) => _app = app;

    private static async Task<Dictionary<string, JsonElement>> Get(HttpClient c) =>
        (await (await c.GetAsync("/api/achievements")).JsonAsync()).EnumerateArray()
            .ToDictionary(a => a.GetProperty("key").GetString()!, a => a);

    private static object Meal(double cal = 500, double pro = 40) =>
        new { foodName = "x", grams = 100, calories = cal, protein = pro, carbs = 1, fat = 1, mealType = "Lunch" };

    [Fact]
    public async Task First_evaluation_is_silent_then_new_unlocks_are_announced_once()
    {
        var (c, _, _, _) = await _app.RegisterAsync("Ilk");
        // Özellik öncesi veri: ilk okuma bunu sessizce açar.
        await c.PostAsJsonAsync("/api/nutrition/log", Meal());
        var first = await Get(c);
        Assert.Equal(1, first["ilk-lokma"].GetProperty("level").GetInt32());
        Assert.False(first["ilk-lokma"].GetProperty("isNew").GetBoolean());

        // Sonraki kazanım duyurulur.
        await c.PostAsJsonAsync("/api/checkin", new { mood = 4, energy = 4, hunger = 3, note = (string?)null, context = "general" });
        var second = await Get(c);
        Assert.Equal(1, second["kendini-dinle"].GetProperty("level").GetInt32());
        Assert.True(second["kendini-dinle"].GetProperty("isNew").GetBoolean());
        Assert.Equal(10, second["kendini-dinle"].GetProperty("nextTarget").GetDouble());

        Assert.Equal(HttpStatusCode.NoContent, (await c.PostAsync("/api/achievements/seen", null)).StatusCode);
        Assert.False((await Get(c))["kendini-dinle"].GetProperty("isNew").GetBoolean());
        // Tekrar okumak yeni satır yazmaz.
        await Get(c);
        Assert.Equal(1, _app.Scalar("SELECT COUNT(*) FROM UserAchievements WHERE Key='kendini-dinle'"
            + " AND UserId = (SELECT Id FROM Users WHERE DisplayName='Ilk')"));
    }

    /// <summary>Verisi olmayan yeni kullanıcının ilk kazandığı başarım kutlanır (ilk değerlendirme boş olsa bile).</summary>
    [Fact]
    public async Task New_user_first_unlock_is_announced()
    {
        var (c, _, _, _) = await _app.RegisterAsync("Yeni");
        Assert.All((await Get(c)).Values, a => Assert.Equal(0, a.GetProperty("level").GetInt32()));
        await Get(c);
        await c.PostAsJsonAsync("/api/nutrition/log", Meal());
        Assert.True((await Get(c))["ilk-lokma"].GetProperty("isNew").GetBoolean());
    }

    [Fact]
    public async Task Unlocked_tier_survives_deleting_the_data()
    {
        var (c, _, _, _) = await _app.RegisterAsync("Kalici");
        await Get(c); // sessiz ilk değerlendirme (boş)
        var meal = await (await c.PostAsJsonAsync("/api/nutrition/log", Meal())).JsonAsync();
        Assert.Equal(1, (await Get(c))["ilk-lokma"].GetProperty("level").GetInt32());
        await c.DeleteAsync($"/api/nutrition/log/{meal.GetProperty("id").GetString()}");
        var after = await Get(c);
        Assert.Equal(1, after["ilk-lokma"].GetProperty("level").GetInt32());
        Assert.Equal(0, after["ilk-lokma"].GetProperty("value").GetDouble());
    }

    private static async Task Session(HttpClient c, string exercise, double kg, int daysAgo)
    {
        var sid = (await (await c.PostAsJsonAsync("/api/workout/session", new { name = "S" })).JsonAsync()).GetProperty("id").GetString()!;
        var eid = (await (await c.PostAsJsonAsync("/api/workout/exercise", new { workoutSessionId = sid, name = exercise, muscleGroup = "X" })).JsonAsync()).GetProperty("id").GetString();
        await c.PostAsJsonAsync("/api/workout/set", new { exerciseId = eid, setNumber = 1, weightKg = kg, reps = 5, isCompleted = true });
        await c.PostAsJsonAsync("/api/workout/set", new { exerciseId = eid, setNumber = 2, weightKg = kg + 50, reps = 5, isCompleted = false }); // tamamlanmamış sayılmaz
        return;
    }

    [Fact]
    public async Task Personal_records_count_only_beating_previous_sessions()
    {
        var (c, id, _, _) = await _app.RegisterAsync("Rekor");
        await Get(c);
        await Session(c, "Squat", 100, 0);
        await Session(c, "Squat", 100, 0);   // eşit: rekor değil
        await Session(c, "squat ", 105, 0);  // ad farkı yok sayılır: rekor
        await Session(c, "Bench", 80, 0);    // ilk kez: rekor değil
        // Seansları sırala: tarihleri farklılaştır.
        var sessions = new List<string>();
        using (var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_app.DbPath};Pooling=False"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id FROM WorkoutSessions WHERE UserId=$u ORDER BY LoggedAt";
            cmd.Parameters.AddWithValue("$u", TestApp.SqlGuid(id));
            using var r = cmd.ExecuteReader();
            while (r.Read()) sessions.Add(r.GetString(0));
        }
        for (var i = 0; i < sessions.Count; i++)
            _app.Exec("UPDATE WorkoutSessions SET LoggedAt=$t WHERE Id=$id",
                ("$t", DateTime.Today.AddDays(-10 + i).ToString("yyyy-MM-dd 18:00:00")), ("$id", sessions[i]));

        var a = await Get(c);
        Assert.Equal(1, a["rekor"].GetProperty("value").GetDouble());
        Assert.Equal(4, a["ilk-ter"].GetProperty("value").GetDouble());
        Assert.Equal(1, a["rekor"].GetProperty("level").GetInt32());
        Assert.Equal(100 * 5 * 2 + 105 * 5 + 80 * 5, a["ton"].GetProperty("value").GetDouble());
    }

    [Fact]
    public async Task Target_weight_progress_and_hint()
    {
        var (c, _, _, _) = await _app.RegisterAsync("Hedef");
        var none = await Get(c);
        Assert.Contains("hedef kilo", none["hedefte"].GetProperty("hint").GetString());

        await c.PutAsJsonAsync("/api/profile", new { heightCm = 180, targetWeightKg = 76 });
        await c.PostAsJsonAsync("/api/weight/log", new { weightKg = 80.0, notes = (string?)null, loggedAt = DateTime.Today.AddDays(-5).ToString("yyyy-MM-dd") + "T08:00:00" });
        await c.PostAsJsonAsync("/api/weight/log", new { weightKg = 77.6, notes = (string?)null, loggedAt = DateTime.Today.ToString("yyyy-MM-dd") + "T08:00:00" });
        var a = await Get(c);
        Assert.Equal(60, a["hedef-yolu"].GetProperty("value").GetDouble());
        Assert.Equal(1, a["hedef-yolu"].GetProperty("level").GetInt32());
        Assert.Equal(0, a["hedefte"].GetProperty("level").GetInt32());
        Assert.Equal(JsonValueKind.Null, a["hedefte"].GetProperty("hint").ValueKind);
    }

    [Fact]
    public async Task Achievements_are_private_and_removed_with_account()
    {
        var (a, aId, _, aPass) = await _app.RegisterAsync("AA");
        var (b, _, _, _) = await _app.RegisterAsync("AB");
        await Get(a);
        await a.PostAsJsonAsync("/api/nutrition/log", Meal());
        Assert.Equal(1, (await Get(a))["ilk-lokma"].GetProperty("level").GetInt32());
        Assert.Equal(0, (await Get(b))["ilk-lokma"].GetProperty("level").GetInt32());

        // B'nin "gördüm" isteği A'nın yeni işaretini kaldırmaz.
        await b.PostAsync("/api/achievements/seen", null);
        Assert.True((await Get(a))["ilk-lokma"].GetProperty("isNew").GetBoolean());

        await a.PostAsJsonAsync("/api/account/delete", new { password = aPass });
        Assert.Equal(0, _app.Scalar("SELECT COUNT(*) FROM UserAchievements WHERE UserId=$u", ("$u", TestApp.SqlGuid(aId))));
    }
}
