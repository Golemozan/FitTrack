using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FitTrack.Tests.Infrastructure;

namespace FitTrack.Tests;

/// <summary>
/// 2026-11-02 güvenlik testinin bulguları (izole üretim kopyasına saldırı raporu). Her biri yeniden
/// açılırsa burada kırmızı yanar.
/// </summary>
public class SecurityRegressionTests : IClassFixture<TestApp>
{
    private readonly TestApp _app;
    public SecurityRegressionTests(TestApp app) => _app = app;

    [Fact]
    public async Task Oversized_text_is_rejected_not_stored()
    {
        var (a, _, _, _) = await _app.RegisterAsync("Uzun");
        var res = await a.PostAsJsonAsync("/api/workout/session", new { name = new string('B', 900_000) });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var note = await a.PostAsJsonAsync("/api/checkin", new { mood = 3, energy = 3, hunger = 3, note = new string('N', 600) });
        Assert.Equal(HttpStatusCode.BadRequest, note.StatusCode);
        var context = await a.PostAsJsonAsync("/api/checkin", new { mood = 3, energy = 3, hunger = 3, context = "<svg onload=alert(1)>" });
        Assert.Equal(HttpStatusCode.BadRequest, context.StatusCode);
    }

    /// <summary>Eskiden kabul ediliyor, sonra özet ve başarım sayfası Infinity yüzünden kalıcı 500 dönüyordu.</summary>
    [Fact]
    public async Task Extreme_numbers_are_rejected_and_summary_stays_healthy()
    {
        var (a, _, _, _) = await _app.RegisterAsync("Uc");
        var sid = (await (await a.PostAsJsonAsync("/api/workout/session", new { name = "S" })).JsonAsync()).GetProperty("id").GetString();
        var eid = (await (await a.PostAsJsonAsync("/api/workout/exercise", new { workoutSessionId = sid, name = "Bench", muscleGroup = "Göğüs" })).JsonAsync()).GetProperty("id").GetString();

        var set = await a.PostAsJsonAsync("/api/workout/set", new { exerciseId = eid, setNumber = -1, weightKg = -1e308, reps = int.MinValue, isCompleted = true });
        Assert.Equal(HttpStatusCode.BadRequest, set.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await a.PutAsJsonAsync("/api/profile", new { heightCm = -10, targetWeightKg = 1e308 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await a.PostAsJsonAsync("/api/nutrition/log",
            new { foodName = "x", grams = -5, calories = 100, protein = 1, carbs = 1, fat = 1, mealType = "Lunch" })).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await a.GetAsync("/api/summary/weekly")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await a.GetAsync("/api/achievements")).StatusCode);
    }

    [Fact]
    public async Task Out_of_range_days_and_dates_do_not_crash()
    {
        var (a, _, _, _) = await _app.RegisterAsync("Gun");
        foreach (var url in new[] { "/api/nutrition/history?days=100000000", "/api/nutrition/history?days=-2147483648", "/api/weight/history?days=100000000" })
            Assert.Equal(HttpStatusCode.OK, (await a.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await a.GetAsync("/api/nutrition/summary?date=9999-12-31")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await a.PostAsJsonAsync("/api/weight/log", new { weightKg = 80, loggedAt = "9999-12-31T00:00:00" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await a.PostAsJsonAsync("/api/nutrition/log",
            new { foodName = "x", grams = 100, calories = 100, protein = 1, carbs = 1, fat = 1, mealType = "Lunch", logDate = "0001-01-01T00:00:00" })).StatusCode);
    }

    [Fact]
    public async Task Undefined_enum_numbers_are_rejected()
    {
        var (a, _, _, _) = await _app.RegisterAsync("Enum");
        var res = await a.PostAsJsonAsync("/api/nutrition/log",
            new { foodName = "x", grams = 100, calories = 100, protein = 1, carbs = 1, fat = 1, mealType = 99 });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        // .NET tip adı ve konum ayrıntısı sızmaz.
        Assert.DoesNotContain("System.", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Daily_quota_caps_rows()
    {
        var (a, _, _, _) = await _app.RegisterAsync("Kota");
        for (var i = 0; i < 10; i++)
            Assert.Equal(HttpStatusCode.Created, (await a.PostAsJsonAsync("/api/workout/session", new { name = $"S{i}" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await a.PostAsJsonAsync("/api/workout/session", new { name = "S10" })).StatusCode);
    }

    [Fact]
    public async Task Location_is_relative_and_host_is_not_reflected()
    {
        var (a, _, _, _) = await _app.RegisterAsync("Host");
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/workout/session") { Content = JsonContent.Create(new { name = "S" }) };
        req.Headers.Host = "evil.example";
        var res = await a.SendAsync(req);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        Assert.DoesNotContain("evil.example", res.Headers.Location?.ToString() ?? "");
    }

    [Fact]
    public async Task Parallel_first_reads_create_one_default_habit()
    {
        var (a, _, _, _) = await _app.RegisterAsync("Yaris");
        await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => a.GetAsync("/api/habits")));
        var habits = await (await a.GetAsync("/api/habits")).JsonAsync();
        Assert.Equal(1, habits.EnumerateArray().Count(h => h.GetProperty("name").GetString() == "Su"));
    }
}
