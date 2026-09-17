using System.Net;
using System.Net.Http.Json;
using FitTrack.Tests.Infrastructure;

namespace FitTrack.Tests;

public class AuthTests : IClassFixture<TestApp>
{
    private readonly TestApp _app;
    public AuthTests(TestApp app) => _app = app;

    [Fact]
    public async Task Api_requires_session()
    {
        var anon = _app.Browser();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/goals")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/account/ai-key")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsJsonAsync("/api/coach/chat", new { messages = Array.Empty<object>() })).StatusCode);
    }

    [Fact]
    public async Task Health_and_unknown_api_paths_are_safe()
    {
        var anon = _app.Browser();
        Assert.Equal(HttpStatusCode.OK, (await anon.GetAsync("/health")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync("/api/does-not-exist")).StatusCode);
        // Tek kullanıcılı dönemin korumasız CRUD uçları kaldırıldı.
        var (user, _, _, _) = await _app.RegisterAsync();
        foreach (var path in new[] { "/api/MealEntries", "/api/WeightLogs", "/api/WorkoutSessions" })
            Assert.Equal(HttpStatusCode.NotFound, (await user.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task Register_then_me_returns_session_without_ai()
    {
        var (client, id, email, _) = await _app.RegisterAsync("Alice");
        var me = await (await client.GetAsync("/api/auth/me")).JsonAsync();
        Assert.Equal(id, me.GetProperty("id").GetGuid());
        Assert.Equal(email, me.GetProperty("email").GetString());
        Assert.Equal("Alice", me.GetProperty("displayName").GetString());
        Assert.False(me.GetProperty("aiEnabled").GetBoolean());
        Assert.False(me.TryGetProperty("passwordHash", out _));
    }

    [Fact]
    public async Task Session_cookie_is_httponly_and_strict()
    {
        var client = _app.Browser();
        var resp = await client.PostAsJsonAsync("/api/auth/register",
            new { email = $"c-{Guid.NewGuid():N}@test.com", password = "cookie-parola-1", displayName = "C" });
        var cookie = resp.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("FitTrack.Session"));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("not-an-email", "gecerli-parola-1", "X", HttpStatusCode.BadRequest)]
    [InlineData("a@b", "gecerli-parola-1", "X", HttpStatusCode.BadRequest)]
    [InlineData("ok@test.com", "kisa", "X", HttpStatusCode.BadRequest)]
    [InlineData("ok@test.com", "aaaaaaaaaaaa", "X", HttpStatusCode.BadRequest)] // tekdüze
    [InlineData("ok@test.com", "gecerli-parola-1", "", HttpStatusCode.BadRequest)]
    [InlineData("ok@test.com", "gecerli-parola-1", "01234567890123456789012345678901234567890", HttpStatusCode.BadRequest)]
    public async Task Register_validates_input(string email, string password, string name, HttpStatusCode expected)
    {
        var resp = await _app.Browser().PostAsJsonAsync("/api/auth/register", new { email, password, displayName = name });
        Assert.Equal(expected, resp.StatusCode);
    }

    [Fact]
    public async Task Register_rejects_password_equal_to_email()
    {
        var email = $"same-{Guid.NewGuid():N}@test.com";
        var resp = await _app.Browser().PostAsJsonAsync("/api/auth/register", new { email, password = email, displayName = "X" });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Duplicate_email_is_case_insensitive()
    {
        var (_, _, email, _) = await _app.RegisterAsync();
        var resp = await _app.Browser().PostAsJsonAsync("/api/auth/register",
            new { email = "  " + email.ToUpperInvariant() + " ", password = "baska-parola-99", displayName = "Evil" });
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    [Fact]
    public async Task Password_is_hashed_not_stored()
    {
        var (_, id, _, password) = await _app.RegisterAsync();
        using var conn = _app.OpenDb();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT PasswordHash FROM Users WHERE Id = $id";
        cmd.Parameters.AddWithValue("$id", TestApp.SqlGuid(id));
        var hash = (string)cmd.ExecuteScalar()!;
        Assert.DoesNotContain(password, hash);
        Assert.True(hash.Length > 60);
    }

    [Fact]
    public async Task Login_logout_cycle()
    {
        var (_, _, email, password) = await _app.RegisterAsync();
        var c = _app.Browser();
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.PostAsJsonAsync("/api/auth/login", new { email, password = "yanlis-parola" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.PostAsJsonAsync("/api/auth/login", new { email = "yok@test.com", password })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.PostAsJsonAsync("/api/auth/login", new { email, password = new string('x', 500) })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.PostAsJsonAsync("/api/auth/login", new { email = email.ToUpperInvariant(), password })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/goals")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await c.PostAsync("/api/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/goals")).StatusCode);
    }

    [Fact]
    public async Task Account_locks_after_five_failures_even_for_correct_password()
    {
        var (_, _, email, password) = await _app.RegisterAsync();
        var c = _app.Browser();
        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await c.PostAsJsonAsync("/api/auth/login", new { email, password = $"yanlis-{i}" })).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await c.PostAsJsonAsync("/api/auth/login", new { email, password })).StatusCode);
    }

    [Fact]
    public async Task Successful_login_resets_failure_counter()
    {
        var (_, _, email, password) = await _app.RegisterAsync();
        var c = _app.Browser();
        for (var i = 0; i < 4; i++) await c.PostAsJsonAsync("/api/auth/login", new { email, password = "yanlis" });
        Assert.Equal(HttpStatusCode.OK, (await c.PostAsJsonAsync("/api/auth/login", new { email, password })).StatusCode);
        for (var i = 0; i < 4; i++) await c.PostAsJsonAsync("/api/auth/login", new { email, password = "yanlis" });
        Assert.Equal(HttpStatusCode.OK, (await c.PostAsJsonAsync("/api/auth/login", new { email, password })).StatusCode);
    }

    [Fact]
    public async Task State_changing_requests_require_csrf_header()
    {
        var noHeader = _app.Browser(csrfHeader: false);
        var resp = await noHeader.PostAsJsonAsync("/api/auth/register",
            new { email = $"csrf-{Guid.NewGuid():N}@test.com", password = "csrf-parola-12", displayName = "X" });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);

        // Oturumlu kullanıcı da başlıksız yazamaz; okuma serbest.
        var (client, _, _, _) = await _app.RegisterAsync();
        var raw = new HttpRequestMessage(HttpMethod.Put, "/api/goals")
        {
            Content = JsonContent.Create(new { calorieGoal = 1, proteinGoal = 1, carbGoal = 1, fatGoal = 1 }),
        };
        client.DefaultRequestHeaders.Remove("X-Requested-With");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(raw)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/goals")).StatusCode);
    }

    [Fact]
    public async Task Password_change_revokes_other_sessions_and_keeps_current()
    {
        var (current, _, email, password) = await _app.RegisterAsync();
        var other = _app.Browser();
        Assert.Equal(HttpStatusCode.OK, (await other.PostAsJsonAsync("/api/auth/login", new { email, password })).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest,
            (await current.PostAsJsonAsync("/api/account/password", new { currentPassword = "yanlis", newPassword = "yeni-parola-12345" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await current.PostAsJsonAsync("/api/account/password", new { currentPassword = password, newPassword = "kisa" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await current.PostAsJsonAsync("/api/account/password", new { currentPassword = password, newPassword = "yeni-parola-12345" })).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await other.GetAsync("/api/goals")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await current.GetAsync("/api/goals")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _app.Browser().PostAsJsonAsync("/api/auth/login", new { email, password })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _app.Browser().PostAsJsonAsync("/api/auth/login", new { email, password = "yeni-parola-12345" })).StatusCode);
    }

    [Fact]
    public async Task Logout_all_revokes_every_session()
    {
        var (a, _, email, password) = await _app.RegisterAsync();
        var b = _app.Browser();
        await b.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.NoContent, (await a.PostAsync("/api/account/logout-all", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await a.GetAsync("/api/goals")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await b.GetAsync("/api/goals")).StatusCode);
    }

    [Fact]
    public async Task Delete_account_requires_password_and_removes_everything()
    {
        var (client, id, email, password) = await _app.RegisterAsync();
        await client.PostAsJsonAsync("/api/nutrition/log", new { foodName = "x", grams = 1, calories = 1, protein = 1, carbs = 1, fat = 1, mealType = "Snack" });
        await client.PostAsJsonAsync("/api/weight/log", new { weightKg = 80 });
        await client.PutAsJsonAsync("/api/account/ai-key", new { key = FakeAnthropic.KeyA });

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/account/delete", new { password = "yanlis" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/account/delete", new { password })).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/goals")).StatusCode);
        var uid = ("$u", (object)TestApp.SqlGuid(id));
        Assert.Equal(0, _app.Scalar("SELECT COUNT(*) FROM Users WHERE Id = $u", uid));
        Assert.Equal(0, _app.Scalar("SELECT COUNT(*) FROM UserApiKeys WHERE UserId = $u", uid));
        Assert.Equal(0, _app.Scalar("SELECT COUNT(*) FROM MealEntries WHERE UserId = $u", uid));
        Assert.Equal(0, _app.Scalar("SELECT COUNT(*) FROM WeightLogs WHERE UserId = $u", uid));
        Assert.Equal(HttpStatusCode.Unauthorized, (await _app.Browser().PostAsJsonAsync("/api/auth/login", new { email, password })).StatusCode);
    }

    [Fact]
    public async Task Security_headers_present()
    {
        var resp = await _app.Browser().GetAsync("/api/auth/me");
        Assert.Equal("nosniff", resp.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", resp.Headers.GetValues("X-Frame-Options").Single());
        Assert.Contains("frame-ancestors 'none'", resp.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Contains("no-store", resp.Headers.CacheControl!.ToString());
    }

    [Fact]
    public async Task Oversized_chat_message_is_rejected()
    {
        var (client, _, _, _) = await _app.RegisterAsync();
        await client.PutAsJsonAsync("/api/account/ai-key", new { key = FakeAnthropic.KeyA });
        // Not: TestServer Kestrel'in 1 MB gövde sınırını uygulamaz; burada uygulama katmanındaki sınır sınanır.
        var huge = new string('x', 1024 * 1024 + 10);
        var resp = await client.PostAsJsonAsync("/api/coach/chat", new { messages = new[] { new { role = "user", content = huge } } });
        Assert.True((int)resp.StatusCode is 400 or 413, $"got {(int)resp.StatusCode}");
    }
}

public class RateLimitApp : TestApp
{
    protected override int AuthLimit => 3;
}

public class RateLimitTests : IClassFixture<RateLimitApp>
{
    private readonly RateLimitApp _app;
    public RateLimitTests(RateLimitApp app) => _app = app;

    [Fact]
    public async Task Auth_endpoints_are_rate_limited_per_ip()
    {
        var c = _app.Browser();
        var codes = new List<HttpStatusCode>();
        for (var i = 0; i < 5; i++)
            codes.Add((await c.PostAsJsonAsync("/api/auth/login", new { email = "nobody@test.com", password = "x" })).StatusCode);
        Assert.Equal(new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized }, codes.Take(3));
        Assert.All(codes.Skip(3), s => Assert.Equal(HttpStatusCode.TooManyRequests, s));
    }
}
