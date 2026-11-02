using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FitTrack.Tests.Infrastructure;

namespace FitTrack.Tests;

/// <summary>Alışkanlıklar, antrenman şablonları ve haftalık özet (2026-11-01).</summary>
public class HabitTemplateSummaryTests : IClassFixture<TestApp>
{
    private readonly TestApp _app;
    public HabitTemplateSummaryTests(TestApp app) => _app = app;

    private static string Day(int daysAgo) => DateTime.Today.AddDays(-daysAgo).ToString("yyyy-MM-dd");

    [Fact]
    public async Task Water_habit_is_created_once_and_counts_per_day()
    {
        var (a, _, _, _) = await _app.RegisterAsync("Su");
        var list = await (await a.GetAsync("/api/habits")).JsonAsync();
        Assert.Equal(1, list.GetArrayLength());
        var water = list[0];
        Assert.Equal("water", water.GetProperty("icon").GetString());
        Assert.Equal(8, water.GetProperty("target").GetInt32());
        var id = water.GetProperty("id").GetString();

        // Arşivlenince varsayılan geri gelmez.
        Assert.Equal(HttpStatusCode.NoContent, (await a.DeleteAsync($"/api/habits/{id}")).StatusCode);
        Assert.Equal(0, (await (await a.GetAsync("/api/habits")).JsonAsync()).GetArrayLength());

        var created = await (await a.PostAsJsonAsync("/api/habits", new { name = "Kreatin", icon = "pill", target = 1 })).JsonAsync();
        var hid = created.GetProperty("id").GetString();
        await a.PutAsJsonAsync($"/api/habits/{hid}/log", new { count = 1 });
        await a.PutAsJsonAsync($"/api/habits/{hid}/log", new { count = 1, date = Day(1) });
        await a.PutAsJsonAsync($"/api/habits/{hid}/log", new { count = 0, date = Day(1) }); // aynı satır güncellenir

        Assert.Equal(1, (await (await a.GetAsync("/api/habits")).JsonAsync())[0].GetProperty("count").GetInt32());
        Assert.Equal(0, (await (await a.GetAsync($"/api/habits?date={Day(1)}")).JsonAsync())[0].GetProperty("count").GetInt32());
        Assert.Equal(2, _app.Scalar("SELECT COUNT(*) FROM HabitLogs WHERE HabitId=$h", ("$h", Guid.Parse(hid!).ToString().ToUpperInvariant())));

        Assert.Equal(HttpStatusCode.BadRequest, (await a.PutAsJsonAsync($"/api/habits/{hid}/log", new { count = 1, date = Day(-1) })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await a.PutAsJsonAsync($"/api/habits/{hid}/log", new { count = 1, date = Day(10) })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await a.PostAsJsonAsync("/api/habits", new { name = "x", icon = "rocket", target = 1 })).StatusCode);
    }

    [Fact]
    public async Task Habits_are_private()
    {
        var (a, _, _, _) = await _app.RegisterAsync("A");
        var (b, _, _, _) = await _app.RegisterAsync("B");
        var hid = (await (await a.PostAsJsonAsync("/api/habits", new { name = "A-vitamin", icon = "pill", target = 1 })).JsonAsync()).GetProperty("id").GetString();

        Assert.DoesNotContain("A-vitamin", await b.GetStringAsync("/api/habits"));
        Assert.Equal(HttpStatusCode.NotFound, (await b.PutAsJsonAsync($"/api/habits/{hid}/log", new { count = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.DeleteAsync($"/api/habits/{hid}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.PutAsJsonAsync($"/api/habits/{hid}", new { name = "hack", icon = "pill", target = 1 })).StatusCode);
        Assert.Contains("A-vitamin", await a.GetStringAsync("/api/habits"));
    }

    private static async Task<string> PushSession(HttpClient c)
    {
        var sid = (await (await c.PostAsJsonAsync("/api/workout/session", new { name = "Push" })).JsonAsync()).GetProperty("id").GetString()!;
        foreach (var (name, kg) in new[] { ("Bench Press", 80.0), ("Shoulder Press", 40.0) })
        {
            var eid = (await (await c.PostAsJsonAsync("/api/workout/exercise", new { workoutSessionId = sid, name, muscleGroup = "Göğüs" })).JsonAsync()).GetProperty("id").GetString();
            for (var n = 1; n <= 3; n++)
                await c.PostAsJsonAsync("/api/workout/set", new { exerciseId = eid, setNumber = n, weightKg = kg + n * 2.5, reps = 8, isCompleted = true });
        }
        return sid;
    }

    [Fact]
    public async Task Template_saves_session_and_applies_with_last_weights()
    {
        var (a, _, _, _) = await _app.RegisterAsync("Sablon");
        var sid = await PushSession(a);

        var template = await (await a.PostAsJsonAsync("/api/workout/templates", new { name = "Push günü", sessionId = sid })).JsonAsync();
        Assert.Equal(2, template.GetProperty("exercises").GetArrayLength());
        Assert.Equal(3, template.GetProperty("exercises")[0].GetProperty("setCount").GetInt32());
        var tid = template.GetProperty("id").GetString();

        // Bugünün seansını sil, şablonu uygula: yeni seans şablon adıyla, setler son ağırlıklarla ve tamamlanmamış.
        await a.DeleteAsync($"/api/workout/session/{sid}");
        var applied = await (await a.PostAsync($"/api/workout/templates/{tid}/apply", null)).JsonAsync();
        Assert.Equal("Push günü", applied.GetProperty("name").GetString());
        // Eski seans silindi; "son yapılan" bulunamaz → 0 kg × hedef tekrar.
        var bench = applied.GetProperty("exercises").EnumerateArray().First(e => e.GetProperty("name").GetString() == "Bench Press");
        Assert.Equal(3, bench.GetProperty("sets").GetArrayLength());
        Assert.All(bench.GetProperty("sets").EnumerateArray(), s => Assert.False(s.GetProperty("isCompleted").GetBoolean()));

        // Tekrar uygulamak aynı hareketleri ikinci kez eklemez.
        var again = await (await a.PostAsync($"/api/workout/templates/{tid}/apply", null)).JsonAsync();
        Assert.Equal(2, again.GetProperty("exercises").GetArrayLength());
    }

    [Fact]
    public async Task Template_prefills_from_previous_session()
    {
        var (a, _, _, _) = await _app.RegisterAsync("Onceki");
        var sid = await PushSession(a);
        var tid = (await (await a.PostAsJsonAsync("/api/workout/templates", new { name = "Push", sessionId = sid })).JsonAsync()).GetProperty("id").GetString();

        // Seansı düne taşı: bugün boş, dünkü seans "son yapılan" olur.
        _app.Exec("UPDATE WorkoutSessions SET LoggedAt = $t WHERE Id = $id",
            ("$t", DateTime.Today.AddDays(-1).AddHours(18).ToString("yyyy-MM-dd HH:mm:ss")), ("$id", Guid.Parse(sid).ToString().ToUpperInvariant()));

        var applied = await (await a.PostAsync($"/api/workout/templates/{tid}/apply", null)).JsonAsync();
        Assert.NotEqual(sid, applied.GetProperty("id").GetString());
        var bench = applied.GetProperty("exercises").EnumerateArray().First(e => e.GetProperty("name").GetString() == "Bench Press");
        var weights = bench.GetProperty("sets").EnumerateArray().Select(s => s.GetProperty("weightKg").GetDouble()).ToList();
        Assert.Equal(new[] { 82.5, 85.0, 87.5 }, weights);
    }

    /// <summary>Hareketler girildiği sırayla gelir — şablon da o sırayı korur (eskiden Guid sırasıyla karışıktı).</summary>
    [Fact]
    public async Task Exercise_order_is_entry_order()
    {
        var (a, _, _, _) = await _app.RegisterAsync("Sira");
        var sid = (await (await a.PostAsJsonAsync("/api/workout/session", new { name = "Sıra" })).JsonAsync()).GetProperty("id").GetString();
        var names = new[] { "Zeta", "Alfa", "Mu", "Beta", "Omega", "Gama" };
        foreach (var n in names)
            await a.PostAsJsonAsync("/api/workout/exercise", new { workoutSessionId = sid, name = n, muscleGroup = "X" });

        string[] Order(JsonElement session) => session.GetProperty("exercises").EnumerateArray().Select(e => e.GetProperty("name").GetString()!).ToArray();
        Assert.Equal(names, Order(await (await a.GetAsync("/api/workout/session/today")).JsonAsync()));

        var template = await (await a.PostAsJsonAsync("/api/workout/templates", new { name = "Sıralı", sessionId = sid })).JsonAsync();
        Assert.Equal(names, template.GetProperty("exercises").EnumerateArray().Select(e => e.GetProperty("name").GetString()!).ToArray());

        await a.DeleteAsync($"/api/workout/session/{sid}");
        var applied = await (await a.PostAsync($"/api/workout/templates/{template.GetProperty("id").GetString()}/apply", null)).JsonAsync();
        Assert.Equal(names, Order(applied));
        Assert.Equal(names, Order(await (await a.GetAsync("/api/workout/session/today")).JsonAsync()));
    }

    /// <summary>Aynı isimle tekrar kaydetmek ikinci kayıt açmaz, mevcut kaydın hareketlerini değiştirir.</summary>
    [Fact]
    public async Task Saving_with_same_name_updates_template()
    {
        var (a, _, _, _) = await _app.RegisterAsync("Guncel");
        var sid = await PushSession(a);
        var first = await (await a.PostAsJsonAsync("/api/workout/templates", new { name = "Push günü", sessionId = sid })).JsonAsync();

        await a.PostAsJsonAsync("/api/workout/exercise", new { workoutSessionId = sid, name = "Dips", muscleGroup = "Göğüs" });
        var second = await (await a.PostAsJsonAsync("/api/workout/templates", new { name = "push GÜNÜ ", sessionId = sid })).JsonAsync();

        Assert.Equal(first.GetProperty("id").GetString(), second.GetProperty("id").GetString());
        Assert.Equal(3, second.GetProperty("exercises").GetArrayLength());
        var list = await (await a.GetAsync("/api/workout/templates")).JsonAsync();
        Assert.Equal(1, list.GetArrayLength());
        Assert.Equal(3, list[0].GetProperty("exercises").GetArrayLength());
    }

    /// <summary>"Güne kaydet": yüklenen antrenmanın tekrarı girilmiş setleri tek seferde tamamlanır.</summary>
    [Fact]
    public async Task Complete_session_marks_filled_sets_done()
    {
        var (a, _, _, _) = await _app.RegisterAsync("Bitir");
        var (b, _, _, _) = await _app.RegisterAsync("BitirB");
        var sid = await PushSession(a);
        var tid = (await (await a.PostAsJsonAsync("/api/workout/templates", new { name = "Push", sessionId = sid })).JsonAsync()).GetProperty("id").GetString();
        _app.Exec("UPDATE WorkoutSessions SET LoggedAt = $t WHERE Id = $id",
            ("$t", DateTime.Today.AddDays(-1).AddHours(18).ToString("yyyy-MM-dd HH:mm:ss")), ("$id", Guid.Parse(sid).ToString().ToUpperInvariant()));

        var applied = await (await a.PostAsync($"/api/workout/templates/{tid}/apply", null)).JsonAsync();
        var today = applied.GetProperty("id").GetString();
        // Boş bir satır (0 tekrar) tamamlanmış sayılmamalı.
        var benchId = applied.GetProperty("exercises")[0].GetProperty("id").GetString();
        await a.PostAsJsonAsync("/api/workout/set", new { exerciseId = benchId, setNumber = 4, weightKg = 0, reps = 0, isCompleted = false });

        Assert.Equal(HttpStatusCode.NotFound, (await b.PostAsync($"/api/workout/session/{today}/complete", null)).StatusCode);

        var done = await (await a.PostAsync($"/api/workout/session/{today}/complete", null)).JsonAsync();
        var sets = done.GetProperty("exercises").EnumerateArray().SelectMany(e => e.GetProperty("sets").EnumerateArray()).ToList();
        Assert.Equal(7, sets.Count);
        Assert.All(sets.Where(s => s.GetProperty("reps").GetInt32() > 0), s => Assert.True(s.GetProperty("isCompleted").GetBoolean()));
        Assert.False(sets.Single(s => s.GetProperty("reps").GetInt32() == 0).GetProperty("isCompleted").GetBoolean());
    }

    [Fact]
    public async Task Templates_are_private()
    {
        var (a, _, _, _) = await _app.RegisterAsync("TA");
        var (b, _, _, _) = await _app.RegisterAsync("TB");
        var sid = await PushSession(a);
        var tid = (await (await a.PostAsJsonAsync("/api/workout/templates", new { name = "Gizli", sessionId = sid })).JsonAsync()).GetProperty("id").GetString();

        Assert.DoesNotContain("Gizli", await b.GetStringAsync("/api/workout/templates"));
        Assert.Equal(HttpStatusCode.NotFound, (await b.PostAsync($"/api/workout/templates/{tid}/apply", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.DeleteAsync($"/api/workout/templates/{tid}")).StatusCode);
        // B, A'nın seansından şablon kaydedemez.
        Assert.Equal(HttpStatusCode.NotFound, (await b.PostAsJsonAsync("/api/workout/templates", new { name = "Çalıntı", sessionId = sid })).StatusCode);
    }

    [Fact]
    public async Task Weekly_summary_counts_this_week_against_previous()
    {
        var (a, _, _, _) = await _app.RegisterAsync("Ozet");
        await a.PutAsJsonAsync("/api/goals", new { calorieGoal = 2000, proteinGoal = 150, carbGoal = 200, fatGoal = 60 });
        foreach (var d in new[] { 0, 1, 2 })
            await a.PostAsJsonAsync("/api/nutrition/log", new { foodName = "x", grams = 100, calories = 2000, protein = 160, carbs = 1, fat = 1, mealType = "Lunch", logDate = Day(d) });
        await a.PostAsJsonAsync("/api/weight/log", new { weightKg = 81.0, loggedAt = Day(9) + "T08:00:00" });
        await a.PostAsJsonAsync("/api/weight/log", new { weightKg = 80.2, loggedAt = Day(0) + "T08:00:00" });
        await PushSession(a);
        var water = (await (await a.GetAsync("/api/habits")).JsonAsync())[0].GetProperty("id").GetString();
        await a.PutAsJsonAsync($"/api/habits/{water}/log", new { count = 8 });
        await a.PutAsJsonAsync($"/api/habits/{water}/log", new { count = 4, date = Day(1) });

        var s = await (await a.GetAsync("/api/summary/weekly")).JsonAsync();
        var w = s.GetProperty("thisWeek");
        Assert.Equal(3, w.GetProperty("mealDays").GetInt32());
        Assert.Equal(3, w.GetProperty("calorieGoalDays").GetInt32());
        Assert.Equal(3, w.GetProperty("proteinGoalDays").GetInt32());
        Assert.Equal(1, w.GetProperty("workouts").GetInt32());
        Assert.Equal(6, w.GetProperty("completedSets").GetInt32());
        Assert.Equal(81.0, w.GetProperty("weightStart").GetDouble());
        Assert.Equal(-0.8, w.GetProperty("weightChange").GetDouble(), 1);
        Assert.Equal(1.7, w.GetProperty("avgWater").GetDouble(), 1); // (8+4)/7
        Assert.Equal(0, s.GetProperty("previousWeek").GetProperty("workouts").GetInt32());
        Assert.Equal(7, s.GetProperty("days").GetArrayLength());
        Assert.Equal(1, s.GetProperty("habits")[0].GetProperty("daysDone").GetInt32());
        Assert.Contains(s.GetProperty("highlights").EnumerateArray(), h => h.GetString()!.Contains("geçen haftadan 1 fazla"));

        var (b, _, _, _) = await _app.RegisterAsync("Bos");
        var empty = await (await b.GetAsync("/api/summary/weekly")).JsonAsync();
        Assert.Equal(0, empty.GetProperty("thisWeek").GetProperty("mealDays").GetInt32());
        Assert.Equal(JsonValueKind.Null, empty.GetProperty("thisWeek").GetProperty("weightChange").ValueKind);
    }

    [Fact]
    public async Task Account_deletion_removes_habits_and_templates()
    {
        var (a, id, _, password) = await _app.RegisterAsync("Silinen");
        var sid = await PushSession(a);
        await a.PostAsJsonAsync("/api/workout/templates", new { name = "Sil", sessionId = sid });
        var water = (await (await a.GetAsync("/api/habits")).JsonAsync())[0].GetProperty("id").GetString();
        await a.PutAsJsonAsync($"/api/habits/{water}/log", new { count = 2 });

        Assert.Equal(HttpStatusCode.NoContent, (await a.PostAsJsonAsync("/api/account/delete", new { password })).StatusCode);
        var u = TestApp.SqlGuid(id);
        foreach (var table in new[] { "Habits", "HabitLogs", "WorkoutTemplates", "TemplateExercises" })
            Assert.Equal(0, _app.Scalar($"SELECT COUNT(*) FROM {table} WHERE UserId=$u", ("$u", u)));
    }
}
