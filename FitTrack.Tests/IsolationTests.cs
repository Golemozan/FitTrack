using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FitTrack.Tests.Infrastructure;

namespace FitTrack.Tests;

/// <summary>Bir kullanıcı, başka bir kullanıcının hiçbir kaydını göremez, değiştiremez, silemez.</summary>
public class IsolationTests : IClassFixture<TestApp>
{
    private readonly TestApp _app;
    public IsolationTests(TestApp app) => _app = app;

    private static object Meal(string name) => new { foodName = name, grams = 100, calories = 150, protein = 12, carbs = 1, fat = 10, mealType = "Breakfast" };

    [Fact]
    public async Task Meals_are_private()
    {
        var (a, _, _, _) = await _app.RegisterAsync("A");
        var (b, _, _, _) = await _app.RegisterAsync("B");

        var created = await (await a.PostAsJsonAsync("/api/nutrition/log", Meal("A-yumurta"))).JsonAsync();
        var id = created.GetProperty("id").GetString();
        Assert.False(created.TryGetProperty("userId", out _));

        Assert.DoesNotContain("A-yumurta", await b.GetStringAsync("/api/nutrition/today"));
        Assert.DoesNotContain("A-yumurta", await b.GetStringAsync($"/api/nutrition/day/{DateTime.Now:yyyy-MM-dd}"));
        Assert.Equal(0, (await (await b.GetAsync("/api/nutrition/summary")).JsonAsync()).GetProperty("totalCalories").GetDouble());
        Assert.Equal("[]", await b.GetStringAsync("/api/nutrition/history?days=3"));
        Assert.Equal(0, (await (await b.GetAsync("/api/nutrition/streak")).JsonAsync()).GetProperty("days").GetInt32());

        Assert.Equal(HttpStatusCode.NotFound, (await b.DeleteAsync($"/api/nutrition/log/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.PutAsJsonAsync($"/api/nutrition/log/{id}", Meal("hacked"))).StatusCode);

        var aToday = await a.GetStringAsync("/api/nutrition/today");
        Assert.Contains("A-yumurta", aToday);
        Assert.DoesNotContain("hacked", aToday);
        Assert.Equal(1, (await (await a.GetAsync("/api/nutrition/streak")).JsonAsync()).GetProperty("days").GetInt32());
        Assert.Equal(150, (await (await a.GetAsync("/api/nutrition/summary")).JsonAsync()).GetProperty("totalCalories").GetDouble());
        Assert.Contains("A-yumurta", await a.GetStringAsync($"/api/nutrition/day/{DateTime.Now:yyyy-MM-dd}"));
        var history = await (await a.GetAsync("/api/nutrition/history?days=3")).JsonAsync();
        Assert.Equal(1, history[0].GetProperty("mealCount").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, (await a.GetAsync("/api/nutrition/day/not-a-date")).StatusCode);
    }

    [Fact]
    public async Task Owner_can_edit_and_delete_own_meal()
    {
        var (a, _, _, _) = await _app.RegisterAsync();
        var id = (await (await a.PostAsJsonAsync("/api/nutrition/log", Meal("ilk"))).JsonAsync()).GetProperty("id").GetString();
        Assert.Equal(HttpStatusCode.OK, (await a.PutAsJsonAsync($"/api/nutrition/log/{id}", Meal("ikinci"))).StatusCode);
        Assert.Contains("ikinci", await a.GetStringAsync("/api/nutrition/today"));
        Assert.Equal(HttpStatusCode.NoContent, (await a.DeleteAsync($"/api/nutrition/log/{id}")).StatusCode);
        Assert.DoesNotContain("ikinci", await a.GetStringAsync("/api/nutrition/today"));
    }

    [Fact]
    public async Task UserId_in_request_body_is_ignored()
    {
        var (a, aId, _, _) = await _app.RegisterAsync("A");
        var (b, bId, _, _) = await _app.RegisterAsync("B");
        await b.PostAsJsonAsync("/api/nutrition/log", new { foodName = "inject", grams = 1, calories = 1, protein = 1, carbs = 1, fat = 1, mealType = "Snack", userId = aId });
        Assert.DoesNotContain("inject", await a.GetStringAsync("/api/nutrition/today"));
        Assert.Equal(1, _app.Scalar("SELECT COUNT(*) FROM MealEntries WHERE FoodName='inject' AND UserId=$u", ("$u", TestApp.SqlGuid(bId))));
    }

    [Fact]
    public async Task Workout_graph_is_private()
    {
        var (a, _, _, _) = await _app.RegisterAsync("A");
        var (b, _, _, _) = await _app.RegisterAsync("B");

        var session = await (await a.PostAsJsonAsync("/api/workout/session", new { name = "A-push" })).JsonAsync();
        var sid = session.GetProperty("id").GetString();
        var ex = await (await a.PostAsJsonAsync("/api/workout/exercise", new { workoutSessionId = sid, name = "Bench A", muscleGroup = "Göğüs" })).JsonAsync();
        var eid = ex.GetProperty("id").GetString();
        var set = await (await a.PostAsJsonAsync("/api/workout/set", new { exerciseId = eid, setNumber = 1, weightKg = 80, reps = 5, isCompleted = true })).JsonAsync();
        var setId = set.GetProperty("id").GetString();

        // B okuyamaz
        Assert.Equal("null", await b.GetStringAsync("/api/workout/session/today"));
        Assert.Equal("[]", await b.GetStringAsync("/api/workout/sessions"));
        Assert.Equal("[]", await b.GetStringAsync("/api/workout/exercise/Bench%20A/history"));

        // B A'nın ağacına ekleyemez
        Assert.Equal(HttpStatusCode.NotFound, (await b.PostAsJsonAsync("/api/workout/exercise", new { workoutSessionId = sid, name = "x", muscleGroup = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.PostAsJsonAsync("/api/workout/set", new { exerciseId = eid, setNumber = 2, weightKg = 1, reps = 1, isCompleted = true })).StatusCode);

        // B A'nın kayıtlarını değiştiremez/silemez
        Assert.Equal(HttpStatusCode.NotFound, (await b.PutAsJsonAsync($"/api/workout/session/{sid}", new { name = "hacked" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.PutAsJsonAsync($"/api/workout/exercise/{eid}", new { name = "hacked", muscleGroup = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.PutAsJsonAsync($"/api/workout/set/{setId}", new { weightKg = 999, reps = 1, isCompleted = true })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.DeleteAsync($"/api/workout/set/{setId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.DeleteAsync($"/api/workout/exercise/{eid}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.DeleteAsync($"/api/workout/session/{sid}")).StatusCode);

        var today = await a.GetStringAsync("/api/workout/session/today");
        Assert.Contains("A-push", today);
        Assert.Contains("Bench A", today);
        Assert.DoesNotContain("hacked", today);
        Assert.DoesNotContain("999", today);

        // Sahibi tam yetkili
        Assert.Equal(HttpStatusCode.OK, (await a.PutAsJsonAsync($"/api/workout/set/{setId}", new { weightKg = 85, reps = 5, isCompleted = true })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await a.PutAsJsonAsync($"/api/workout/exercise/{eid}", new { name = "Bench B", muscleGroup = "Göğüs" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await a.PutAsJsonAsync($"/api/workout/session/{sid}", new { name = "Push" })).StatusCode);
        Assert.Single((await (await a.GetAsync("/api/workout/sessions")).JsonAsync()).EnumerateArray());
        Assert.Equal(HttpStatusCode.NoContent, (await a.DeleteAsync($"/api/workout/set/{setId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await a.DeleteAsync($"/api/workout/exercise/{eid}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await a.DeleteAsync($"/api/workout/session/{sid}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await a.GetAsync("/api/workout/exercises/list")).StatusCode);
    }

    [Fact]
    public async Task Weight_is_private()
    {
        var (a, _, _, _) = await _app.RegisterAsync("A");
        var (b, _, _, _) = await _app.RegisterAsync("B");
        var log = await (await a.PostAsJsonAsync("/api/weight/log", new { weightKg = 91.5, notes = "A" })).JsonAsync();
        var id = log.GetProperty("id").GetString();
        // aynı gün ikinci kayıt üzerine yazar
        await a.PostAsJsonAsync("/api/weight/log", new { weightKg = 91.0, notes = "A2" });
        await a.PostAsJsonAsync("/api/weight/log", new { weightKg = 92.0, loggedAt = DateTime.Now.AddDays(-8) });

        var bStats = await (await b.GetAsync("/api/weight/stats")).JsonAsync();
        Assert.Equal(JsonValueKind.Null, bStats.GetProperty("currentWeight").ValueKind);
        Assert.Equal("null", await b.GetStringAsync("/api/weight/today"));
        Assert.Equal("[]", await b.GetStringAsync("/api/weight/history"));
        Assert.Equal("[]", await b.GetStringAsync("/api/weight/logs"));
        Assert.Equal(HttpStatusCode.NotFound, (await b.PutAsJsonAsync($"/api/weight/log/{id}", new { weightKg = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.DeleteAsync($"/api/weight/log/{id}")).StatusCode);

        var aStats = await (await a.GetAsync("/api/weight/stats")).JsonAsync();
        Assert.Equal(91.0, aStats.GetProperty("currentWeight").GetDouble());
        Assert.Equal(-1.0, aStats.GetProperty("weeklyChange").GetDouble(), 3);
        Assert.Equal(2, (await (await a.GetAsync("/api/weight/logs")).JsonAsync()).GetArrayLength());
        Assert.Equal(HttpStatusCode.OK, (await a.PutAsJsonAsync($"/api/weight/log/{id}", new { weightKg = 90.0 })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await a.DeleteAsync($"/api/weight/log/{id}")).StatusCode);
    }

    [Fact]
    public async Task Checkins_goals_profile_are_private()
    {
        var (a, _, _, _) = await _app.RegisterAsync("A");
        var (b, _, _, _) = await _app.RegisterAsync("B");

        var ci = await (await a.PostAsJsonAsync("/api/checkin", new { mood = 9, energy = 0, hunger = 3, note = "A-not" })).JsonAsync();
        Assert.Equal(5, ci.GetProperty("mood").GetInt32()); // 1-5'e kırpılır
        Assert.Equal("[]", await b.GetStringAsync("/api/checkin/today"));
        Assert.Equal("[]", await b.GetStringAsync("/api/checkin/recent"));
        Assert.Equal(HttpStatusCode.NotFound, (await b.DeleteAsync($"/api/checkin/{ci.GetProperty("id").GetString()}")).StatusCode);
        Assert.Contains("A-not", await a.GetStringAsync("/api/checkin/recent"));
        Assert.Equal(HttpStatusCode.NoContent, (await a.DeleteAsync($"/api/checkin/{ci.GetProperty("id").GetString()}")).StatusCode);

        await a.PutAsJsonAsync("/api/goals", new { calorieGoal = 1111, proteinGoal = 1, carbGoal = 1, fatGoal = 1 });
        await a.PutAsJsonAsync("/api/profile", new { heightCm = 181, targetWeightKg = 77 });
        var bGoals = await (await b.GetAsync("/api/goals")).JsonAsync();
        var bProfile = await (await b.GetAsync("/api/profile")).JsonAsync();
        Assert.Equal(2500, bGoals.GetProperty("calorieGoal").GetDouble());
        Assert.Equal(JsonValueKind.Null, bProfile.GetProperty("heightCm").ValueKind);
        Assert.Equal(1111, (await (await a.GetAsync("/api/goals")).JsonAsync()).GetProperty("calorieGoal").GetDouble());
        Assert.Equal(181, (await (await a.GetAsync("/api/profile")).JsonAsync()).GetProperty("heightCm").GetDouble());

        // Her kullanıcının kendi tekil satırı: aynı Id çakışması yok.
        Assert.NotEqual(bGoals.GetProperty("id").GetString(), (await (await a.GetAsync("/api/goals")).JsonAsync()).GetProperty("id").GetString());
    }
}
