using System.Net;
using System.Net.Http.Json;
using FitTrack.API.Data;
using FitTrack.API.Services;
using FitTrack.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace FitTrack.Tests;

// 2026-11-06 denetiminin bulguları (hız sınırı, büyüme, giriş kilidi, kayıt kotası, istemci IP'si).
// Telegram bulguları TelegramTests, TelegramQueueTests ve PasswordResetTests'te.

/// <summary>Hiçbir sınır ezilmez: üretimin varsayılanları (dakikada 300 / yazmada 60, IP başına saatte 5 kayıt).</summary>
public class DefaultLimitsApp : TestApp
{
    protected override int? GlobalLimit => null;
    protected override int? GlobalWriteLimit => null;
    protected override int? RegisterIpLimit => null;
}

public class DefaultLimitTests : IClassFixture<DefaultLimitsApp>
{
    private readonly DefaultLimitsApp _app;
    public DefaultLimitTests(DefaultLimitsApp app) => _app = app;

    private async Task<HttpClient> Register(string ip)
    {
        var client = _app.Browser();
        client.DefaultRequestHeaders.Add(TestApp.TestClientIpHeader, ip);
        var resp = await client.PostAsJsonAsync("/api/auth/register",
            new { email = $"v-{Guid.NewGuid():N}@test.com", password = "varsayilan-parola-1", displayName = "V" });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        return client;
    }

    [Fact]
    public async Task Writes_stop_at_60_a_minute_with_the_usual_error_shape()
    {
        var a = await Register("198.51.100.60");
        var codes = new List<HttpStatusCode>();
        for (var i = 0; i < 60; i++) // kayıt IP kovasına yazıldı, bunlar kullanıcı kovasına; satır da eklenmez
            codes.Add((await a.PostAsJsonAsync("/api/checkin", new { mood = 3, energy = 3, hunger = 3, context = "invalid-context" })).StatusCode);
        Assert.All(codes, c => Assert.Equal(HttpStatusCode.BadRequest, c)); // kuraldan döner, limitten değil

        var rejected = await a.PostAsJsonAsync("/api/checkin", new { mood = 3, energy = 3, hunger = 3, context = "invalid-context" });
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Contains("Çok fazla istek", (await rejected.JsonAsync()).GetProperty("error").GetString());

        // Okuma kovası ayrı: yazma dolu olsa da kullanıcı veriyi görür.
        Assert.Equal(HttpStatusCode.OK, (await a.GetAsync("/api/goals")).StatusCode);
    }

    [Fact]
    public async Task Requests_stop_at_300_a_minute_and_per_user()
    {
        var a = await Register("198.51.100.61");
        var b = await Register("198.51.100.62");
        for (var i = 0; i < 300; i++)
            Assert.Equal(HttpStatusCode.OK, (await a.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await a.GetAsync("/api/auth/me")).StatusCode);

        // Başka kullanıcı ve muaf yollar etkilenmez.
        Assert.Equal(HttpStatusCode.OK, (await b.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await a.GetAsync("/health")).StatusCode);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, (await a.GetAsync("/index.html")).StatusCode);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, (await a.GetAsync("/assets/app.js")).StatusCode);
    }

    [Fact]
    public async Task One_ip_registers_five_times_an_hour_by_default()
    {
        var codes = new List<HttpStatusCode>();
        for (var i = 0; i < 6; i++)
        {
            var client = _app.Browser();
            client.DefaultRequestHeaders.Add(TestApp.TestClientIpHeader, "198.51.100.63");
            codes.Add((await client.PostAsJsonAsync("/api/auth/register",
                new { email = $"s-{Guid.NewGuid():N}@test.com", password = "varsayilan-parola-2", displayName = "S" })).StatusCode);
        }
        Assert.Equal(Enumerable.Repeat(HttpStatusCode.OK, 5), codes.Take(5));
        Assert.Equal(HttpStatusCode.TooManyRequests, codes[5]);
    }
}

public class LowGlobalLimitApp : TestApp
{
    protected override int? GlobalLimit => 5;
    protected override int? GlobalWriteLimit => 2;
}

/// <summary>Sınırlar yapılandırmadan ezilebilir; anonim istek IP'ye, oturumlu istek kullanıcıya yazılır.</summary>
public class GlobalLimiterOverrideTests : IClassFixture<LowGlobalLimitApp>
{
    private readonly LowGlobalLimitApp _app;
    public GlobalLimiterOverrideTests(LowGlobalLimitApp app) => _app = app;

    [Fact]
    public async Task Anonymous_requests_are_partitioned_by_ip()
    {
        HttpRequestMessage Req(string ip)
        {
            var req = new HttpRequestMessage(HttpMethod.Get, "/api/does-not-exist");
            req.Headers.Add(TestApp.TestClientIpHeader, ip);
            return req;
        }
        var anon = _app.Browser();
        for (var i = 0; i < 5; i++)
            Assert.NotEqual(HttpStatusCode.TooManyRequests, (await anon.SendAsync(Req("203.0.113.70"))).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await anon.SendAsync(Req("203.0.113.70"))).StatusCode);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, (await anon.SendAsync(Req("203.0.113.71"))).StatusCode);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task Every_unsafe_method_uses_the_write_bucket(string method)
    {
        var client = _app.Browser();
        client.DefaultRequestHeaders.Add(TestApp.TestClientIpHeader, $"203.0.113.{80 + method.Length + method[0]}");
        var codes = new List<HttpStatusCode>();
        for (var i = 0; i < 3; i++)
            codes.Add((await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), "/api/does-not-exist"))).StatusCode);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, codes[0]);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, codes[1]);
        Assert.Equal(HttpStatusCode.TooManyRequests, codes[2]);
    }
}

public class SqlCaptureApp : TestApp
{
    protected override bool CaptureLogs => true;
}

/// <summary>Büyüme: alışkanlık ve satır tavanları, özetlerin SQL'de çalışması, sohbet temizliği.</summary>
public class GrowthTests : IClassFixture<SqlCaptureApp>
{
    private readonly SqlCaptureApp _app;
    public GrowthTests(SqlCaptureApp app) => _app = app;

    /// <summary>Kaydı API'nin 10 yılına yayılmış saymak uzun sürer: tavana kadar doğrudan yazılır.</summary>
    private void Seed(string table, string columns, string values, Guid user, int rows) => _app.Exec(
        $"""
        PRAGMA foreign_keys=OFF;
        WITH RECURSIVE c(n) AS (SELECT 1 UNION ALL SELECT n + 1 FROM c WHERE n < {rows})
        INSERT INTO {table} (Id, UserId, {columns}) SELECT lower(hex(randomblob(16))), $u, {values} FROM c;
        """, ("$u", TestApp.SqlGuid(user)));

    private const string PastDay = "strftime('%Y-%m-%d 12:00:00', 'now', 'localtime', '-' || ((n % 3000) + 1) || ' days')";

    private List<string> SqlDuring() =>
        _app.Logs.Lines.Where(l => l.Category == "Microsoft.EntityFrameworkCore.Database.Command").Select(l => l.Message).ToList();

    private static async Task<string> ErrorOf(HttpResponseMessage resp)
    {
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        return (await resp.JsonAsync()).GetProperty("error").GetString()!;
    }

    [Fact]
    public async Task Habits_are_capped_at_200_including_archive()
    {
        var (a, id, _, _) = await _app.RegisterAsync("Arsiv");
        var habit = new { name = "Yeni", icon = "check", target = 1 };
        Seed("Habits", "Name, Icon, Target, SortOrder, IsArchived, CreatedAt", "'Eski', 'check', 1, n, 1, '2026-01-01 00:00:00'", id, DataRules.MaxHabitsTotal - 1);

        var last = await a.PostAsJsonAsync("/api/habits", habit);
        Assert.Equal(HttpStatusCode.OK, last.StatusCode);
        var lastId = (await last.JsonAsync()).GetProperty("id").GetString();
        Assert.Contains("200", await ErrorOf(await a.PostAsJsonAsync("/api/habits", habit)));

        // Arşivlemek yer açmaz, ama eldeki alışkanlık çalışmaya devam eder.
        Assert.Equal(HttpStatusCode.OK, (await a.PutAsJsonAsync($"/api/habits/{lastId}", new { name = "Yeni ad", icon = "check", target = 2 })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await a.DeleteAsync($"/api/habits/{lastId}")).StatusCode);
        Assert.Contains("200", await ErrorOf(await a.PostAsJsonAsync("/api/habits", habit)));
        Assert.Equal(DataRules.MaxHabitsTotal, _app.Scalar("SELECT COUNT(*) FROM Habits WHERE UserId=$u", ("$u", TestApp.SqlGuid(id))));

        var (b, _, _, _) = await _app.RegisterAsync("Komsu");
        Assert.Equal(HttpStatusCode.OK, (await b.PostAsJsonAsync("/api/habits", habit)).StatusCode);
    }

    [Fact]
    public async Task Total_row_caps_stop_new_rows_but_keep_existing_ones_editable()
    {
        var (a, id, _, _) = await _app.RegisterAsync("Tavan");
        var meal = new { foodName = "x", grams = 1, calories = 1, protein = 1, carbs = 1, fat = 1, mealType = "Lunch" };

        Seed("MealEntries", "FoodName, Grams, Calories, Protein, Carbs, Fat, MealType, LoggedAt", $"'Eski', 1, 1, 1, 1, 1, 1, {PastDay}", id, DataRules.MaxMealsTotal - 1);
        var mealResp = await a.PostAsJsonAsync("/api/nutrition/log", meal);
        Assert.Equal(HttpStatusCode.Created, mealResp.StatusCode);
        var mealId = (await mealResp.JsonAsync()).GetProperty("id").GetString();
        Assert.Contains("50000", await ErrorOf(await a.PostAsJsonAsync("/api/nutrition/log", meal)));
        Assert.Equal(HttpStatusCode.OK, (await a.PutAsJsonAsync($"/api/nutrition/log/{mealId}", meal)).StatusCode);

        Seed("WorkoutSessions", "Name, LoggedAt", $"'Eski', {PastDay}", id, DataRules.MaxWorkoutsTotal - 1);
        var session = await a.PostAsJsonAsync("/api/workout/session", new { name = "S" });
        Assert.Equal(HttpStatusCode.Created, session.StatusCode);
        var sessionId = (await session.JsonAsync()).GetProperty("id").GetString();
        Assert.Contains("10000", await ErrorOf(await a.PostAsJsonAsync("/api/workout/session", new { name = "S" })));

        Seed("CheckIns", "Mood, Energy, Hunger, Context, LoggedAt", $"3, 3, 3, 'general', {PastDay}", id, DataRules.MaxCheckInsTotal - 1);
        var checkin = new { mood = 3, energy = 3, hunger = 3 };
        Assert.Equal(HttpStatusCode.OK, (await a.PostAsJsonAsync("/api/checkin", checkin)).StatusCode);
        Assert.Contains("20000", await ErrorOf(await a.PostAsJsonAsync("/api/checkin", checkin)));

        Seed("Exercises", "WorkoutSessionId, Name, MuscleGroup", $"'{Guid.Empty}', 'Eski', 'Kol'", id, DataRules.MaxExercisesTotal - 1);
        var exercise = new { workoutSessionId = sessionId, name = "Bench", muscleGroup = "Göğüs" };
        var exerciseResp = await a.PostAsJsonAsync("/api/workout/exercise", exercise);
        Assert.True(exerciseResp.IsSuccessStatusCode);
        var exerciseId = (await exerciseResp.JsonAsync()).GetProperty("id").GetString();
        Assert.Contains("50000", await ErrorOf(await a.PostAsJsonAsync("/api/workout/exercise", exercise)));

        Seed("ExerciseSets", "ExerciseId, SetNumber, WeightKg, Reps, IsCompleted", $"'{Guid.Empty}', 1, 10, 5, 1", id, DataRules.MaxSetsTotal - 1);
        var set = new { exerciseId, setNumber = 1, weightKg = 10, reps = 5, isCompleted = true };
        Assert.True((await a.PostAsJsonAsync("/api/workout/set", set)).IsSuccessStatusCode);
        Assert.Contains("250000", await ErrorOf(await a.PostAsJsonAsync("/api/workout/set", set)));

        // Tavan kullanıcı başına: komşu etkilenmez.
        var (b, _, _, _) = await _app.RegisterAsync("KomsuTavan");
        Assert.Equal(HttpStatusCode.Created, (await b.PostAsJsonAsync("/api/nutrition/log", meal)).StatusCode);
    }

    [Fact]
    public async Task Nutrition_history_is_grouped_in_sql_with_the_same_response()
    {
        var (a, _, _, _) = await _app.RegisterAsync("Gecmis");
        var today = DateTime.Now.Date;
        async Task Log(DateTime when, double cal, double pro, double carb, double fat) =>
            Assert.Equal(HttpStatusCode.Created, (await a.PostAsJsonAsync("/api/nutrition/log", new
            {
                foodName = "x", grams = 1, calories = cal, protein = pro, carbs = carb, fat, mealType = "Lunch",
                logDate = when.ToString("yyyy-MM-ddTHH:mm:ss"),
            })).StatusCode);
        await Log(today.AddDays(-2).AddHours(9), 100, 10, 20, 3);
        await Log(today.AddDays(-2).AddHours(20), 50.5, 1, 2, 0.5);
        await Log(today.AddHours(0), 200, 20, 30, 5);
        await Log(today.AddDays(-40), 999, 9, 9, 9); // pencere dışı

        _app.Logs.Clear();
        var rows = (await (await a.GetAsync("/api/nutrition/history?days=7")).JsonAsync()).EnumerateArray().ToList();
        var sql = SqlDuring().Where(s => s.Contains("\"MealEntries\"")).ToList();

        Assert.NotEmpty(sql);
        Assert.All(sql, s =>
        {
            Assert.Contains("GROUP BY", s);
            Assert.DoesNotContain("\"FoodName\"", s); // satırlar belleğe alınmıyor
        });

        Assert.Equal(2, rows.Count);
        Assert.Equal(today.AddDays(-2).ToString("yyyy-MM-ddTHH:mm:ss"), rows[0].GetProperty("date").GetString());
        Assert.Equal(150.5, rows[0].GetProperty("totalCalories").GetDouble());
        Assert.Equal(11, rows[0].GetProperty("totalProtein").GetDouble());
        Assert.Equal(22, rows[0].GetProperty("totalCarbs").GetDouble());
        Assert.Equal(3.5, rows[0].GetProperty("totalFat").GetDouble());
        Assert.Equal(2, rows[0].GetProperty("mealCount").GetInt32());
        Assert.Equal(today.ToString("yyyy-MM-ddTHH:mm:ss"), rows[1].GetProperty("date").GetString());
        Assert.Equal(1, rows[1].GetProperty("mealCount").GetInt32());
    }

    [Fact]
    public async Task Weekly_summary_filters_archived_habits_in_sql()
    {
        var (a, id, _, _) = await _app.RegisterAsync("Hafta");
        var u = TestApp.SqlGuid(id);
        var today = DateTime.Now.Date;
        void Habit(string habitId, string name, string icon, int archived) => _app.Exec(
            "INSERT INTO Habits (Id, UserId, Name, Icon, Target, SortOrder, IsArchived, CreatedAt) VALUES ($i, $u, $n, $c, 2, 0, $a, $t)",
            ("$i", habitId), ("$u", u), ("$n", name), ("$c", icon), ("$a", archived), ("$t", today.AddDays(-60).ToString("yyyy-MM-dd HH:mm:ss")));
        void Log(string habitId, DateTime day, int count) => _app.Exec(
            "INSERT INTO HabitLogs (Id, UserId, HabitId, Day, Count) VALUES ($i, $u, $h, $d, $c)",
            ("$i", Guid.NewGuid().ToString().ToUpperInvariant()), ("$u", u), ("$h", habitId), ("$d", day.ToString("yyyy-MM-dd HH:mm:ss")), ("$c", count));

        const string active = "AAAAAAAA-1111-4111-8111-000000000001";
        const string archivedInPeriod = "AAAAAAAA-1111-4111-8111-000000000002";
        const string archivedOld = "AAAAAAAA-1111-4111-8111-000000000003";
        Habit(active, "Aktif", "steps", 0);
        Habit(archivedInPeriod, "Bu hafta bitti", "book", 1);
        Habit(archivedOld, "Cok eski", "pill", 1);
        Log(archivedInPeriod, today, 2);
        Log(archivedOld, today.AddDays(-30), 2);

        _app.Logs.Clear();
        var s = await (await a.GetAsync("/api/summary/weekly")).JsonAsync();
        var habitSql = SqlDuring().Where(q => q.Contains("FROM \"Habits\"")).ToList();

        var query = Assert.Single(habitSql);
        Assert.Contains("\"HabitLogs\"", query);
        Assert.Contains("\"IsArchived\"", query);

        // Arşivli alışkanlık listede yok; dönemde logu varsa oranda sayılır: 14 slot, 1 tam gün.
        var listed = s.GetProperty("habits").EnumerateArray().Select(h => h.GetProperty("name").GetString()).ToList();
        Assert.Equal(new[] { "Aktif" }, listed);
        Assert.Equal(Math.Round(1.0 / 14, 3), s.GetProperty("thisWeek").GetProperty("habitRate").GetDouble());
        // Önceki haftada yalnız aktif alışkanlık kaldı: eski arşiv sayılmaz.
        Assert.Equal(0, s.GetProperty("previousWeek").GetProperty("habitRate").GetDouble());
    }

    [Fact]
    public async Task Coach_messages_are_cleaned_hourly_not_only_at_startup()
    {
        var (_, id, _, _) = await _app.RegisterAsync("Temizlik");
        Assert.Contains(typeof(CoachMessageCleanupService), _app.RegisteredBackgroundServices);

        var cleanup = ActivatorUtilities.CreateInstance<CoachMessageCleanupService>(_app.Services);
        Assert.Equal(TimeSpan.FromHours(1), cleanup.Interval);

        void Message(string messageId, string createdAt) => _app.Exec(
            "INSERT INTO CoachMessages (Id, UserId, Role, Content, CreatedAt) VALUES ($i, $u, 'user', 'x', $t)",
            ("$i", messageId), ("$u", TestApp.SqlGuid(id)), ("$t", createdAt));
        const string oldId = "BBBBBBBB-1111-4111-8111-000000000001";
        const string freshId = "BBBBBBBB-1111-4111-8111-000000000002";
        Message(oldId, DateTime.UtcNow.AddDays(-4).ToString("yyyy-MM-dd HH:mm:ss"));
        Message(freshId, DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd HH:mm:ss"));

        cleanup.Interval = TimeSpan.FromMilliseconds(30);
        await cleanup.StartAsync(CancellationToken.None);
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (_app.Scalar("SELECT COUNT(*) FROM CoachMessages WHERE Id=$i", ("$i", oldId)) > 0 && DateTime.UtcNow < deadline)
            await Task.Delay(20);
        await cleanup.StopAsync(CancellationToken.None);

        Assert.Equal(0, _app.Scalar("SELECT COUNT(*) FROM CoachMessages WHERE Id=$i", ("$i", oldId)));
        Assert.Equal(1, _app.Scalar("SELECT COUNT(*) FROM CoachMessages WHERE Id=$i", ("$i", freshId)));
    }
}

/// <summary>Yavaş damla: son başarısız denemeden 15 dakika geçince hesap sayacı sıfırdan başlar.</summary>
public class FailedLoginDecayTests : IClassFixture<TestApp>
{
    private readonly TestApp _app;
    public FailedLoginDecayTests(TestApp app) => _app = app;

    private async Task<HttpResponseMessage> LoginFrom(string ip, string email, string password)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login") { Content = JsonContent.Create(new { email, password }) };
        req.Headers.Add(TestApp.TestClientIpHeader, ip);
        return await _app.Browser().SendAsync(req);
    }

    private void SetCounter(Guid id, int count, string? lastFailureSql) => _app.Exec(
        $"UPDATE Users SET FailedLoginCount=$c, LastFailedLoginAt={lastFailureSql ?? "NULL"} WHERE Id=$u",
        ("$c", count), ("$u", TestApp.SqlGuid(id)));

    private long Count(Guid id) => _app.Scalar("SELECT FailedLoginCount FROM Users WHERE Id=$u", ("$u", TestApp.SqlGuid(id)));
    private bool Locked(Guid id) => _app.Scalar("SELECT COUNT(*) FROM Users WHERE Id=$u AND LockoutUntil IS NOT NULL", ("$u", TestApp.SqlGuid(id))) == 1;

    private const string SixteenMinutesAgo = "strftime('%Y-%m-%d %H:%M:%f', 'now', '-16 minutes')";
    private const string FiveMinutesAgo = "strftime('%Y-%m-%d %H:%M:%f', 'now', '-5 minutes')";

    [Fact]
    public async Task Counter_restarts_after_15_quiet_minutes_so_slow_drip_cannot_lock()
    {
        var (_, id, email, password) = await _app.RegisterAsync("Damla");
        SetCounter(id, 29, SixteenMinutesAgo);

        await LoginFrom("198.51.100.1", email, "yanlis-parola-1");

        Assert.Equal(1, Count(id));
        Assert.False(Locked(id));
        Assert.Equal(HttpStatusCode.OK, (await LoginFrom("203.0.113.1", email, password)).StatusCode); // yeni cihazdan
    }

    [Fact]
    public async Task Rapid_distributed_failures_still_lock()
    {
        var (_, id, email, password) = await _app.RegisterAsync("Hizli");
        SetCounter(id, 29, FiveMinutesAgo);

        await LoginFrom("198.51.100.2", email, "yanlis-parola-1");

        Assert.True(Locked(id));
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginFrom("203.0.113.2", email, password)).StatusCode);
    }

    /// <summary>Canlıdaki eski satırlarda zaman damgası yok: sayaç birikmiş sayılmaz, kimse durduk yere kilitlenmez.</summary>
    [Fact]
    public async Task Old_rows_without_timestamp_start_from_zero()
    {
        var (_, id, email, _) = await _app.RegisterAsync("Eski");
        SetCounter(id, 29, null);

        await LoginFrom("198.51.100.3", email, "yanlis-parola-1");

        Assert.Equal(1, Count(id));
        Assert.False(Locked(id));
    }

    [Fact]
    public async Task Successful_login_clears_the_timestamp()
    {
        var (_, id, email, password) = await _app.RegisterAsync("Temiz");
        await LoginFrom("198.51.100.4", email, "yanlis-parola-1");
        Assert.Equal(1, _app.Scalar("SELECT COUNT(*) FROM Users WHERE Id=$u AND LastFailedLoginAt IS NOT NULL", ("$u", TestApp.SqlGuid(id))));

        Assert.Equal(HttpStatusCode.OK, (await LoginFrom("198.51.100.4", email, password)).StatusCode);

        Assert.Equal(0, _app.Scalar("SELECT COUNT(*) FROM Users WHERE Id=$u AND LastFailedLoginAt IS NOT NULL", ("$u", TestApp.SqlGuid(id))));
    }

    /// <summary>Canlı veritabanında sütun yok: açılıştaki yükseltme veriyi silmeden ekler, giriş çalışır.</summary>
    [Fact]
    public async Task Schema_upgrade_adds_the_column_to_an_existing_database()
    {
        var app = new TestApp();
        try
        {
            var (_, id, email, password) = await app.RegisterAsync("Sema");
            app.Exec("ALTER TABLE Users DROP COLUMN LastFailedLoginAt");

            using (var scope = app.Services.CreateScope())
                SchemaUpgrade.Run(scope.ServiceProvider.GetRequiredService<AppDbContext>());

            Assert.Equal(1, app.Scalar("SELECT COUNT(*) FROM pragma_table_info('Users') WHERE name='LastFailedLoginAt'"));
            Assert.Equal(1, app.Scalar("SELECT COUNT(*) FROM Users WHERE Id=$u AND LastFailedLoginAt IS NULL", ("$u", TestApp.SqlGuid(id))));
            Assert.Equal(HttpStatusCode.OK, (await app.Browser().PostAsJsonAsync("/api/auth/login", new { email, password })).StatusCode);

            using (var scope = app.Services.CreateScope()) // ikinci açılış da sorunsuz
                SchemaUpgrade.Run(scope.ServiceProvider.GetRequiredService<AppDbContext>());
        }
        finally { app.Dispose(); }
    }
}

public class RegistrationQuotaApp : TestApp
{
    protected override int? RegisterIpLimit => 2;
    protected override int RegisterLimit => 5;
}

/// <summary>Tek IP sunucu geneli kayıt kotasını tüketemez; IP'ye takılan istek genel kotadan düşmez.</summary>
public class RegistrationQuotaTests : IClassFixture<RegistrationQuotaApp>
{
    private readonly RegistrationQuotaApp _app;
    public RegistrationQuotaTests(RegistrationQuotaApp app) => _app = app;

    private Task<HttpResponseMessage> Register(string ip, string? email = null, string password = "kota-parolasi-44")
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/auth/register")
        {
            Content = JsonContent.Create(new { email = email ?? $"k-{Guid.NewGuid():N}@test.com", password, displayName = "K" }),
        };
        req.Headers.Add(TestApp.TestClientIpHeader, ip);
        return _app.Browser().SendAsync(req);
    }

    [Fact]
    public async Task One_ip_cannot_exhaust_the_server_wide_quota()
    {
        // Saldırgan IP: 2 hesap açar, sonrası reddedilir. Reddedilenler genel 5'lik kotadan düşmez.
        Assert.Equal(HttpStatusCode.OK, (await Register("198.51.100.90")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Register("198.51.100.90")).StatusCode);
        for (var i = 0; i < 6; i++)
        {
            var rejected = await Register("198.51.100.90");
            Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
            Assert.False(string.IsNullOrEmpty((await rejected.JsonAsync()).GetProperty("error").GetString()));
        }

        // Meşru kullanıcılar kayıt olabilir. Hatalı form ve var olan e-posta IP kotasını yemez.
        var taken = $"var-{Guid.NewGuid():N}@test.com";
        Assert.Equal(HttpStatusCode.BadRequest, (await Register("198.51.100.91", password: "kisa")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Register("198.51.100.91", password: "kisa")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Register("198.51.100.91", taken)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Register("198.51.100.91", taken)).StatusCode); // kabul edilen risk: 409 kalır
        Assert.Equal(HttpStatusCode.OK, (await Register("198.51.100.91")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Register("198.51.100.92")).StatusCode);

        // Genel tavan hâlâ geçerli: 5 hesap açıldı, yeni IP de artık giremez.
        Assert.Equal(HttpStatusCode.TooManyRequests, (await Register("198.51.100.93")).StatusCode);
    }
}

public class RegisterAuthLimitApp : TestApp
{
    protected override int AuthLimit => 3;
}

public class RegisterAuthLimitTests : IClassFixture<RegisterAuthLimitApp>
{
    private readonly RegisterAuthLimitApp _app;
    public RegisterAuthLimitTests(RegisterAuthLimitApp app) => _app = app;

    /// <summary>Saatlik IP sınırı dakikalık Auth korumasının yerine geçmedi; 409 numaralandırması onunla sınırlı.</summary>
    [Fact]
    public async Task Register_keeps_the_per_minute_auth_limit()
    {
        var c = _app.Browser();
        c.DefaultRequestHeaders.Add(TestApp.TestClientIpHeader, "198.51.100.95");
        var codes = new List<HttpStatusCode>();
        for (var i = 0; i < 4; i++)
            codes.Add((await c.PostAsJsonAsync("/api/auth/register", new { email = "gecersiz", password = "x", displayName = "x" })).StatusCode);
        Assert.Equal(Enumerable.Repeat(HttpStatusCode.BadRequest, 3), codes.Take(3));
        Assert.Equal(HttpStatusCode.TooManyRequests, codes[3]);
    }
}

public class ProductionIpApp : TestApp
{
    protected override string EnvironmentName => "Production";
    protected override int AuthLimit => 3;
}

/// <summary>Üretimde istemci IP'si X-Real-IP'den gelir ve hız sınırından önce uygulanır; X-Forwarded-For sayılmaz.</summary>
public class ProductionClientIpTests : IClassFixture<ProductionIpApp>
{
    private readonly ProductionIpApp _app;
    public ProductionClientIpTests(ProductionIpApp app) => _app = app;

    private HttpClient Plain() => _app.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("http://localhost"), AllowAutoRedirect = false, HandleCookies = false,
    });

    private static string SeenIp(HttpResponseMessage resp) => resp.Headers.GetValues(TestApp.SeenIpHeader).Single();

    private async Task<string?> Seen(params (string Name, string Value)[] headers)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "/health");
        foreach (var (n, v) in headers) req.Headers.TryAddWithoutValidation(n, v);
        return SeenIp(await Plain().SendAsync(req));
    }

    [Theory]
    [InlineData("203.0.113.5", "203.0.113.5")]
    [InlineData(" 203.0.113.5 ", "203.0.113.5")]
    [InlineData("2001:db8::5", "2001:db8::5")]
    [InlineData("::ffff:203.0.113.5", "203.0.113.5")]
    public async Task Valid_real_ip_becomes_the_remote_address(string header, string expected) =>
        Assert.Equal(expected, await Seen(("X-Real-IP", header)));

    [Theory]
    [InlineData("kotu-deger")]
    [InlineData("203.0.113.5, 198.51.100.1")]
    [InlineData("1")]
    [InlineData("010.0.0.1")]
    [InlineData("[2001:db8::5]:443")]
    [InlineData("fe80::1%1")]
    [InlineData("203.0.113.5:443")]
    public async Task Invalid_real_ip_keeps_the_connection_address(string header)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "/health");
        req.Headers.Add(TestApp.TestClientIpHeader, "192.0.2.10");
        req.Headers.TryAddWithoutValidation("X-Real-IP", header);
        Assert.Equal("192.0.2.10", SeenIp(await Plain().SendAsync(req)));
    }

    [Fact]
    public async Task Missing_real_ip_keeps_the_connection_address_and_ignores_forwarded_for()
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "/health");
        req.Headers.Add(TestApp.TestClientIpHeader, "192.0.2.11");
        req.Headers.Add("X-Forwarded-For", "198.51.100.200");
        Assert.Equal("192.0.2.11", SeenIp(await Plain().SendAsync(req)));
    }

    [Fact]
    public async Task Repeated_real_ip_header_is_invalid()
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "/health");
        req.Headers.Add(TestApp.TestClientIpHeader, "192.0.2.12");
        req.Headers.TryAddWithoutValidation("X-Real-IP", new[] { "203.0.113.5", "203.0.113.6" });
        Assert.Equal("192.0.2.12", SeenIp(await Plain().SendAsync(req)));
    }

    [Fact]
    public async Task Forwarded_proto_still_sets_https()
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "/health");
        req.Headers.Add("X-Forwarded-Proto", "https");
        var resp = await Plain().SendAsync(req);
        Assert.Equal("https", resp.Headers.GetValues(TestApp.SeenSchemeHeader).Single());
        Assert.Equal("http", (await Plain().GetAsync("/health")).Headers.GetValues(TestApp.SeenSchemeHeader).Single());
    }

    /// <summary>Altın kural: X-Real-IP adımı limiter'dan önce. Sahte X-Forwarded-For kovayı değiştiremez, X-Real-IP değiştirir.</summary>
    [Fact]
    public async Task Rate_limit_partitions_on_real_ip_and_forwarded_for_cannot_dodge_it()
    {
        async Task<HttpStatusCode> Login(string realIp, string forwarded)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
            {
                Content = JsonContent.Create(new { email = "nobody@test.com", password = "x" }),
            };
            req.Headers.Add(FitTrack.API.Security.CsrfHeaderMiddleware.HeaderName, FitTrack.API.Security.CsrfHeaderMiddleware.HeaderValue);
            req.Headers.Add("X-Real-IP", realIp);
            req.Headers.Add("X-Forwarded-For", forwarded);
            return (await Plain().SendAsync(req)).StatusCode;
        }

        for (var i = 0; i < 3; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, await Login("203.0.113.30", $"198.51.100.{i}"));
        Assert.Equal(HttpStatusCode.TooManyRequests, await Login("203.0.113.30", "198.51.100.99"));
        Assert.Equal(HttpStatusCode.Unauthorized, await Login("203.0.113.31", "198.51.100.99"));
    }

    /// <summary>Gerçek tarayıcı akışı (HTTPS, __Host- çerez) yeni adımla bozulmadı.</summary>
    [Fact]
    public async Task Host_cookie_session_still_works_behind_the_edge()
    {
        var c = _app.Browser();
        c.DefaultRequestHeaders.Add("X-Real-IP", "203.0.113.40");
        c.DefaultRequestHeaders.Add("X-Forwarded-Proto", "https");
        var resp = await c.PostAsJsonAsync("/api/auth/register",
            new { email = $"e-{Guid.NewGuid():N}@test.com", password = "kenar-parola-123", displayName = "E" });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Contains(resp.Headers.GetValues("Set-Cookie"), v => v.StartsWith("__Host-FitTrack="));
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/auth/me")).StatusCode);
    }
}

public class DevelopmentIpApp : TestApp
{
    protected override int AuthLimit => 3;
}

/// <summary>Geliştirme ortamında Railway kenarı yok: X-Real-IP'ye güvenilmez, kova değişmez.</summary>
public class DevelopmentClientIpTests : IClassFixture<DevelopmentIpApp>
{
    private readonly DevelopmentIpApp _app;
    public DevelopmentClientIpTests(DevelopmentIpApp app) => _app = app;

    [Fact]
    public async Task Real_ip_header_is_not_trusted()
    {
        var c = _app.Browser();
        var req = new HttpRequestMessage(HttpMethod.Get, "/health");
        req.Headers.Add(TestApp.TestClientIpHeader, "192.0.2.20");
        req.Headers.Add("X-Real-IP", "203.0.113.5");
        Assert.Equal("192.0.2.20", (await c.SendAsync(req)).Headers.GetValues(TestApp.SeenIpHeader).Single());

        var codes = new List<HttpStatusCode>();
        for (var i = 0; i < 4; i++)
        {
            var login = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login") { Content = JsonContent.Create(new { email = "nobody@test.com", password = "x" }) };
            login.Headers.Add("X-Real-IP", $"203.0.113.{i + 1}");
            codes.Add((await c.SendAsync(login)).StatusCode);
        }
        Assert.Equal(HttpStatusCode.TooManyRequests, codes[3]);
    }
}
