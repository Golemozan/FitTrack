using System.Net;
using System.Net.Http.Json;
using System.Text;
using FitTrack.Tests.Infrastructure;

namespace FitTrack.Tests;

public class ApiKeyTests : IClassFixture<TestApp>
{
    private readonly TestApp _app;
    public ApiKeyTests(TestApp app) => _app = app;

    [Fact]
    public async Task No_key_by_default()
    {
        var (c, _, _, _) = await _app.RegisterAsync();
        var s = await (await c.GetAsync("/api/account/ai-key")).JsonAsync();
        Assert.False(s.GetProperty("hasKey").GetBoolean());
        Assert.True(s.GetProperty("storageAvailable").GetBoolean());
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("sk-ant-has space inside-xxxxxxxxxxxx")]
    public async Task Malformed_key_is_rejected_without_calling_anthropic(string key)
    {
        var (c, _, _, _) = await _app.RegisterAsync();
        var before = _app.Anthropic.Calls.Count;
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PutAsJsonAsync("/api/account/ai-key", new { key })).StatusCode);
        Assert.Equal(before, _app.Anthropic.Calls.Count);
    }

    [Fact]
    public async Task Key_rejected_by_anthropic_is_not_stored()
    {
        var (c, id, _, _) = await _app.RegisterAsync();
        var resp = await c.PutAsJsonAsync("/api/account/ai-key", new { key = "sk-ant-invalid-xxxxxxxxxxxxxxxxxxxxxxxx" });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Equal(0, _app.Scalar("SELECT COUNT(*) FROM UserApiKeys WHERE UserId=$u", ("$u", TestApp.SqlGuid(id))));
    }

    [Fact]
    public async Task Unreachable_anthropic_does_not_store_key()
    {
        var app = new TestApp();
        try
        {
            var (c, id, _, _) = await app.RegisterAsync();
            app.Anthropic.Unreachable = true;
            Assert.Equal(HttpStatusCode.BadGateway, (await c.PutAsJsonAsync("/api/account/ai-key", new { key = FakeAnthropic.KeyA })).StatusCode);
            Assert.Equal(0, app.Scalar("SELECT COUNT(*) FROM UserApiKeys WHERE UserId=$u", ("$u", TestApp.SqlGuid(id))));
        }
        finally { app.Dispose(); }
    }

    [Fact]
    public async Task Valid_key_is_encrypted_at_rest_and_never_returned()
    {
        var (c, id, _, _) = await _app.RegisterAsync();
        var resp = await c.PutAsJsonAsync("/api/account/ai-key", new { key = "  " + FakeAnthropic.KeyA + "  " });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();

        // Anthropic'e doğrulama çağrısı kırpılmış anahtarla gitti
        Assert.Contains(_app.Anthropic.Calls, x => x.Path == "/v1/models" && x.Key == FakeAnthropic.KeyA);

        // Cevaplarda anahtar yok, yalnız ipucu
        Assert.DoesNotContain(FakeAnthropic.KeyA, body);
        Assert.DoesNotContain(FakeAnthropic.KeyA[8..^4], body);
        var status = await c.GetStringAsync("/api/account/ai-key");
        Assert.DoesNotContain(FakeAnthropic.KeyA[8..^4], status);
        Assert.Equal("sk-ant-…a1a1", System.Text.Json.JsonDocument.Parse(status).RootElement.GetProperty("hint").GetString());
        Assert.DoesNotContain(FakeAnthropic.KeyA[8..^4], await c.GetStringAsync("/api/auth/me"));

        // Veritabanında şifreli; düz metin dosyanın hiçbir yerinde yok
        using (var conn = _app.OpenDb())
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT Ciphertext FROM UserApiKeys WHERE UserId=$u";
            cmd.Parameters.AddWithValue("$u", TestApp.SqlGuid(id));
            var cipher = (string)cmd.ExecuteScalar()!;
            Assert.StartsWith("v1:", cipher);
            Assert.DoesNotContain(FakeAnthropic.KeyA, cipher);
        }
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        var raw = await File.ReadAllBytesAsync(_app.DbPath);
        Assert.Equal(-1, IndexOf(raw, Encoding.UTF8.GetBytes(FakeAnthropic.KeyA[8..^4])));

        var me = await (await c.GetAsync("/api/auth/me")).JsonAsync();
        Assert.True(me.GetProperty("aiEnabled").GetBoolean());
    }

    [Fact]
    public async Task Replace_and_delete_key()
    {
        var (c, id, _, _) = await _app.RegisterAsync();
        await c.PutAsJsonAsync("/api/account/ai-key", new { key = FakeAnthropic.KeyA });
        await c.PutAsJsonAsync("/api/account/ai-key", new { key = FakeAnthropic.KeyB });
        Assert.Equal(1, _app.Scalar("SELECT COUNT(*) FROM UserApiKeys WHERE UserId=$u", ("$u", TestApp.SqlGuid(id))));
        Assert.Contains("b2b2", await c.GetStringAsync("/api/account/ai-key"));

        Assert.Equal(HttpStatusCode.NoContent, (await c.DeleteAsync("/api/account/ai-key")).StatusCode);
        Assert.False((await (await c.GetAsync("/api/auth/me")).JsonAsync()).GetProperty("aiEnabled").GetBoolean());
        Assert.Equal(0, _app.Scalar("SELECT COUNT(*) FROM UserApiKeys WHERE UserId=$u", ("$u", TestApp.SqlGuid(id))));
    }

    [Fact]
    public async Task Ciphertext_copied_to_another_user_cannot_be_used()
    {
        var (a, aId, _, _) = await _app.RegisterAsync("A");
        var (b, bId, _, _) = await _app.RegisterAsync("B");
        await a.PutAsJsonAsync("/api/account/ai-key", new { key = FakeAnthropic.KeyA });

        // Veritabanına yazabilen saldırgan: A'nın şifreli anahtarını B'nin satırına kopyalar.
        _app.Exec("INSERT INTO UserApiKeys (UserId, Ciphertext, Hint, CreatedAt) SELECT $b, Ciphertext, Hint, CreatedAt FROM UserApiKeys WHERE UserId=$a",
            ("$a", TestApp.SqlGuid(aId)), ("$b", TestApp.SqlGuid(bId)));

        var callsBefore = _app.Anthropic.Calls.Count(x => x.Path == "/v1/messages");
        var resp = await b.PostAsJsonAsync("/api/coach/chat", new { messages = new[] { new { role = "user", content = "selam" } } });
        Assert.NotEqual(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal(callsBefore, _app.Anthropic.Calls.Count(x => x.Path == "/v1/messages"));
    }

    private static int IndexOf(byte[] haystack, byte[] needle)
    {
        for (var i = 0; i <= haystack.Length - needle.Length; i++)
            if (haystack.AsSpan(i, needle.Length).SequenceEqual(needle)) return i;
        return -1;
    }
}

/// <summary>Üretimde şifreleme anahtarı unutulursa anahtar saklama tamamen kapanır (fail-closed).</summary>
public class NoEncryptionKeyApp : TestApp
{
    protected override string EnvironmentName => "Production";
    protected override bool WithEncryptionKey => false;
}

public class NoEncryptionKeyTests : IClassFixture<NoEncryptionKeyApp>
{
    private readonly NoEncryptionKeyApp _app;
    public NoEncryptionKeyTests(NoEncryptionKeyApp app) => _app = app;

    [Fact]
    public async Task Key_storage_is_disabled_in_production_without_master_key()
    {
        var (c, _, _, _) = await _app.RegisterAsync();
        var status = await (await c.GetAsync("/api/account/ai-key")).JsonAsync();
        Assert.False(status.GetProperty("storageAvailable").GetBoolean());
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await c.PutAsJsonAsync("/api/account/ai-key", new { key = FakeAnthropic.KeyA })).StatusCode);
        var resp = await c.PostAsJsonAsync("/api/coach/chat", new { messages = new[] { new { role = "user", content = "selam" } } });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Production_cookie_uses_host_prefix_and_secure_and_hsts()
    {
        var client = _app.Browser();
        var resp = await client.PostAsJsonAsync("/api/auth/register",
            new { email = $"p-{Guid.NewGuid():N}@test.com", password = "prod-parola-123", displayName = "P" });
        var cookie = resp.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("__Host-FitTrack"));
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("domain=", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.True(resp.Headers.Contains("Strict-Transport-Security"));
    }
}
