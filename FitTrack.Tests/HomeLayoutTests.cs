using System.Net;
using System.Net.Http.Json;
using FitTrack.Tests.Infrastructure;

namespace FitTrack.Tests;

/// <summary>Ana sayfanın özelleştirilebilir kart düzeni (2026-11-02).</summary>
public class HomeLayoutTests : IClassFixture<TestApp>
{
    private readonly TestApp _app;
    public HomeLayoutTests(TestApp app) => _app = app;

    private static async Task<string[]> Types(HttpClient c) =>
        (await (await c.GetAsync("/api/home-layout")).JsonAsync()).GetProperty("widgets").EnumerateArray()
            .Select(w => w.GetProperty("type").GetString()!).ToArray();

    [Fact]
    public async Task Default_then_saved_layout_round_trips()
    {
        var (c, _, _, _) = await _app.RegisterAsync("Duzen");
        Assert.Equal(new[] { "mood", "habits", "tiles", "tip", "coach" }, await Types(c));

        var put = await c.PutAsJsonAsync("/api/home-layout", new
        {
            widgets = new object[]
            {
                new { id = "n1", type = "note", text = "  Şekerli içecek yok  " },
                new { id = "tip", type = "tip", topics = new[] { "microplastic", "sleep" } },
                new { id = "habits", type = "habits" },
            },
        });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var body = await (await c.GetAsync("/api/home-layout")).JsonAsync();
        var w = body.GetProperty("widgets");
        Assert.Equal(new[] { "note", "tip", "habits" }, w.EnumerateArray().Select(x => x.GetProperty("type").GetString()).ToArray());
        Assert.Equal("Şekerli içecek yok", w[0].GetProperty("text").GetString());
        Assert.Equal(2, w[1].GetProperty("topics").GetArrayLength());

        // Boş düzen de geçerli: kullanıcı her kartı kaldırabilir.
        Assert.Equal(HttpStatusCode.OK, (await c.PutAsJsonAsync("/api/home-layout", new { widgets = Array.Empty<object>() })).StatusCode);
        Assert.Empty(await Types(c));
    }

    [Fact]
    public async Task Invalid_layouts_are_rejected()
    {
        var (c, _, _, _) = await _app.RegisterAsync("Gecersiz");
        async Task<HttpStatusCode> Put(params object[] widgets) => (await c.PutAsJsonAsync("/api/home-layout", new { widgets })).StatusCode;

        Assert.Equal(HttpStatusCode.BadRequest, await Put(new { id = "x", type = "weather" }));
        Assert.Equal(HttpStatusCode.BadRequest, await Put(new { id = "a", type = "mood" }, new { id = "b", type = "mood" }));
        Assert.Equal(HttpStatusCode.BadRequest, await Put(new { id = "a", type = "note" }, new { id = "a", type = "note" }));
        Assert.Equal(HttpStatusCode.BadRequest, await Put(new { id = "n", type = "note", text = new string('x', 281) }));
        Assert.Equal(HttpStatusCode.BadRequest, await Put(new { id = "t", type = "tip", topics = new[] { "astroloji" } }));
        Assert.Equal(HttpStatusCode.BadRequest, await Put(Enumerable.Range(0, 6).Select(i => (object)new { id = $"n{i}", type = "note", text = "x" }).ToArray()));
        Assert.Equal(new[] { "mood", "habits", "tiles", "tip", "coach" }, await Types(c)); // hiçbiri yazılmadı
    }

    [Fact]
    public async Task Layout_is_private_and_removed_with_account()
    {
        var (a, aId, _, aPass) = await _app.RegisterAsync("LA");
        var (b, _, _, _) = await _app.RegisterAsync("LB");
        await a.PutAsJsonAsync("/api/home-layout", new { widgets = new[] { new { id = "n", type = "note", text = "A-gizli-not" } } });

        Assert.DoesNotContain("A-gizli-not", await b.GetStringAsync("/api/home-layout"));
        Assert.Contains("A-gizli-not", await a.GetStringAsync("/api/home-layout"));

        await a.PostAsJsonAsync("/api/account/delete", new { password = aPass });
        Assert.Equal(0, _app.Scalar("SELECT COUNT(*) FROM HomeLayouts WHERE UserId=$u", ("$u", TestApp.SqlGuid(aId))));
    }
}
