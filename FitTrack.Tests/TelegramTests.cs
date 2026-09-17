using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FitTrack.Tests.Infrastructure;

namespace FitTrack.Tests;

public class TelegramApp : TestApp
{
    protected override bool TelegramEnabled => true;
}

/// <summary>
/// Telegram bir kimlik doğrulama yolu: sohbet, hesaba yalnız web'den alınan tek kullanımlık kodla bağlanır.
/// Bot döngüsü çalıştırılmaz; güncellemeler doğrudan <c>HandleUpdate</c>'e verilir, Telegram API'si sahte.
/// </summary>
public class TelegramTests : IClassFixture<TelegramApp>
{
    private readonly TelegramApp _app;
    private readonly FitTrack.API.Services.TelegramBotService _bot; // canlıdaki gibi tek örnek: deneme sayacı onun içinde

    public TelegramTests(TelegramApp app)
    {
        _app = app;
        _bot = app.TelegramBot();
    }

    private static long NewChat() => Random.Shared.NextInt64(1_000_000, long.MaxValue / 2);

    private Task Say(long chatId, string text, string type = "private") =>
        _bot.HandleUpdate(TestApp.TelegramToken, new JsonObject
        {
            ["update_id"] = 1,
            ["message"] = new JsonObject
            {
                ["text"] = text,
                ["chat"] = new JsonObject { ["id"] = chatId, ["type"] = type },
            },
        }, CancellationToken.None);

    private async Task<string> CodeFor(HttpClient web)
    {
        var resp = await web.PostAsync("/api/account/telegram/link-code", null);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        return (await resp.JsonAsync()).GetProperty("code").GetString()!;
    }

    [Fact]
    public async Task Unlinked_chat_gets_instructions_and_touches_nothing()
    {
        var chat = NewChat();
        var before = _app.Anthropic.Calls.Count;
        await Say(chat, "bugün 3 yumurta yedim");
        Assert.Contains(_app.Anthropic.SentTo(chat), t => t.Contains("bağlı değil"));
        Assert.Equal(before, _app.Anthropic.Calls.Count);
        Assert.Equal(0, _app.Scalar("SELECT COUNT(*) FROM CoachMessages WHERE Content LIKE '%yumurta%'"));
    }

    [Fact]
    public async Task Group_chats_are_refused()
    {
        var chat = NewChat();
        await Say(chat, "/link ABCDEFGH", type: "group");
        Assert.Contains(_app.Anthropic.SentTo(chat), t => t.Contains("özel sohbet"));
    }

    [Fact]
    public async Task Link_flow_single_use_code()
    {
        var (web, userId, _, _) = await _app.RegisterAsync("Tele");
        Assert.False((await (await web.GetAsync("/api/account/telegram")).JsonAsync()).GetProperty("linked").GetBoolean());
        var code = await CodeFor(web);
        var sql = "SELECT TelegramLinkCodeHash FROM Users WHERE Id=$u";
        using (var conn = _app.OpenDb())
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = sql;
            cmd.Parameters.AddWithValue("$u", TestApp.SqlGuid(userId));
            Assert.DoesNotContain(code, (string)cmd.ExecuteScalar()!); // kodun kendisi saklanmaz
        }

        var chat = NewChat();
        await Say(chat, "/link YANLIS99");
        Assert.Contains(_app.Anthropic.SentTo(chat), t => t.Contains("geçersiz"));

        await Say(chat, $"/link@FitTrackBot  {code.ToLowerInvariant()} ");
        Assert.Contains(_app.Anthropic.SentTo(chat), t => t.Contains("Bağlandı, Tele"));
        Assert.True((await (await web.GetAsync("/api/account/telegram")).JsonAsync()).GetProperty("linked").GetBoolean());

        // Aynı kod ikinci kez kullanılamaz
        var intruder = NewChat();
        await Say(intruder, $"/start {code}");
        Assert.Contains(_app.Anthropic.SentTo(intruder), t => t.Contains("geçersiz"));
        Assert.Equal(chat, _app.Scalar("SELECT TelegramChatId FROM Users WHERE Id=$u", ("$u", TestApp.SqlGuid(userId))));

        await Say(chat, "/start");
        Assert.Contains(_app.Anthropic.SentTo(chat), t => t.Contains("Koç hazır"));

        await Say(chat, "/unlink");
        Assert.False((await (await web.GetAsync("/api/account/telegram")).JsonAsync()).GetProperty("linked").GetBoolean());
        await Say(chat, "selam");
        Assert.Contains(_app.Anthropic.SentTo(chat), t => t.Contains("bağlı değil"));
    }

    [Fact]
    public async Task Expired_code_is_rejected()
    {
        var (web, userId, _, _) = await _app.RegisterAsync();
        var code = await CodeFor(web);
        _app.Exec("UPDATE Users SET TelegramLinkCodeExpiresAt = '2000-01-01 00:00:00' WHERE Id=$u", ("$u", TestApp.SqlGuid(userId)));
        var chat = NewChat();
        await Say(chat, $"/link {code}");
        Assert.Contains(_app.Anthropic.SentTo(chat), t => t.Contains("geçersiz"));
    }

    [Fact]
    public async Task Brute_force_is_limited_per_chat()
    {
        var (web, _, _, _) = await _app.RegisterAsync();
        var chat = NewChat();
        for (var i = 0; i < 5; i++) await Say(chat, $"/link WRONG00{i}");
        var code = await CodeFor(web);
        await Say(chat, $"/link {code}"); // 6. deneme doğru kod bile olsa reddedilir
        Assert.Contains(_app.Anthropic.SentTo(chat), t => t.Contains("Çok fazla deneme"));
        Assert.DoesNotContain(_app.Anthropic.SentTo(chat), t => t.StartsWith("Bağlandı"));
    }

    [Fact]
    public async Task Relinking_a_chat_moves_it_to_the_new_account()
    {
        var (a, aId, _, _) = await _app.RegisterAsync("A");
        var (b, bId, _, _) = await _app.RegisterAsync("B");
        var chat = NewChat();
        await Say(chat, $"/link {await CodeFor(a)}");
        await Say(chat, $"/link {await CodeFor(b)}");
        Assert.Equal(0, _app.Scalar("SELECT COUNT(*) FROM Users WHERE Id=$u AND TelegramChatId IS NOT NULL", ("$u", TestApp.SqlGuid(aId))));
        Assert.Equal(chat, _app.Scalar("SELECT TelegramChatId FROM Users WHERE Id=$u", ("$u", TestApp.SqlGuid(bId))));
    }

    [Fact]
    public async Task Web_unlink_endpoint()
    {
        var (web, _, _, _) = await _app.RegisterAsync();
        var chat = NewChat();
        await Say(chat, $"/link {await CodeFor(web)}");
        Assert.Equal(HttpStatusCode.NoContent, (await web.DeleteAsync("/api/account/telegram")).StatusCode);
        await Say(chat, "selam");
        Assert.Contains(_app.Anthropic.SentTo(chat), t => t.Contains("bağlı değil"));
    }

    [Fact]
    public async Task Linked_chat_uses_only_its_owners_key_and_data()
    {
        var (a, aId, _, _) = await _app.RegisterAsync("A");
        var (b, _, _, _) = await _app.RegisterAsync("B");
        var chat = NewChat();
        await Say(chat, $"/link {await CodeFor(a)}");

        // Anahtar yokken AI'ye gitmez
        var before = _app.Anthropic.Calls.Count(x => x.Path == "/v1/messages");
        await Say(chat, "yumurta yedim");
        Assert.Contains(_app.Anthropic.SentTo(chat), t => t.Contains("API anahtarını eklemen"));
        Assert.Equal(before, _app.Anthropic.Calls.Count(x => x.Path == "/v1/messages"));

        await a.PutAsJsonAsync("/api/account/ai-key", new { key = FakeAnthropic.KeyA });
        await b.PutAsJsonAsync("/api/account/ai-key", new { key = FakeAnthropic.KeyB });
        await Say(chat, "TG-A-mesaji");
        var call = _app.Anthropic.Calls.Last(x => x.Path == "/v1/messages");
        Assert.Equal(FakeAnthropic.KeyA, call.Key);
        Assert.Contains(_app.Anthropic.SentTo(chat), t => t == "Koç cevabı");
        Assert.Contains("TG-A-mesaji", await a.GetStringAsync("/api/coach/history"));
        Assert.DoesNotContain("TG-A-mesaji", await b.GetStringAsync("/api/coach/history"));
        Assert.Equal(1, _app.Scalar("SELECT COUNT(*) FROM CoachMessages WHERE Content='TG-A-mesaji' AND UserId=$u", ("$u", TestApp.SqlGuid(aId))));
    }

    [Fact]
    public async Task Revoked_key_is_reported_in_telegram()
    {
        var (a, _, _, _) = await _app.RegisterAsync();
        var chat = NewChat();
        await Say(chat, $"/link {await CodeFor(a)}");
        const string key = "sk-ant-test-TGREVOKEDTGREVOKEDTGREVOKED-t9t9";
        _app.Anthropic.ValidKeys.Add(key);
        await a.PutAsJsonAsync("/api/account/ai-key", new { key });
        _app.Anthropic.RevokedKeys.Add(key);
        await Say(chat, "selam");
        Assert.Contains(_app.Anthropic.SentTo(chat), t => t.Contains("reddetti"));
    }

    [Fact]
    public async Task Daily_nudge_and_summary_are_per_user()
    {
        var (withMeal, withMealId, _, _) = await _app.RegisterAsync("Yemekli");
        var (noMeal, noMealId, _, _) = await _app.RegisterAsync("Yemeksiz");
        var (noKey, noKeyId, _, _) = await _app.RegisterAsync("Anahtarsiz");
        long c1 = NewChat(), c2 = NewChat(), c3 = NewChat();
        await Say(c1, $"/link {await CodeFor(withMeal)}");
        await Say(c2, $"/link {await CodeFor(noMeal)}");
        await Say(c3, $"/link {await CodeFor(noKey)}");

        await withMeal.PostAsJsonAsync("/api/nutrition/log", new { foodName = "Gece-yemek", grams = 1, calories = 1, protein = 1, carbs = 1, fat = 1, mealType = "Dinner" });
        await noKey.PostAsJsonAsync("/api/nutrition/log", new { foodName = "x", grams = 1, calories = 1, protein = 1, carbs = 1, fat = 1, mealType = "Dinner" });
        await withMeal.PutAsJsonAsync("/api/account/ai-key", new { key = FakeAnthropic.KeyA });
        await noMeal.PutAsJsonAsync("/api/account/ai-key", new { key = FakeAnthropic.KeyB });

        var coach = _app.DailyCoach();
        var linked = await coach.LinkedUsersAsync(CancellationToken.None);
        Assert.Contains(linked, x => x.Item1 == withMealId && x.Item2 == c1);

        foreach (var (id, chat) in new[] { (withMealId, c1), (noMealId, c2), (noKeyId, c3) })
        {
            await coach.TryNudgeAsync(TestApp.TelegramToken, id, chat, CancellationToken.None);
            await coach.TryNudgeAsync(TestApp.TelegramToken, id, chat, CancellationToken.None); // ikinci kez gönderilmez
        }
        Assert.DoesNotContain(_app.Anthropic.SentTo(c1), t => t.Contains("hiç öğün kaydı yok"));
        Assert.Single(_app.Anthropic.SentTo(c2), t => t.Contains("hiç öğün kaydı yok"));
        Assert.DoesNotContain(_app.Anthropic.SentTo(c3), t => t.Contains("hiç öğün kaydı yok"));

        var before = _app.Anthropic.Calls.Count(x => x.Path == "/v1/messages");
        foreach (var (id, chat) in new[] { (withMealId, c1), (noMealId, c2), (noKeyId, c3) })
            await coach.TrySummaryAsync(TestApp.TelegramToken, id, chat, CancellationToken.None);

        var summaryCalls = _app.Anthropic.Calls.Where(x => x.Path == "/v1/messages").Skip(before).ToList();
        var single = Assert.Single(summaryCalls); // veri yok (noMeal) ya da anahtar yok (noKey) → çağrı yok
        Assert.Equal(FakeAnthropic.KeyA, single.Key);
        var toolNames = single.Body!["tools"]!.AsArray().Select(t => (string)t!["name"]!).ToList();
        Assert.DoesNotContain("log_food", toolNames); // gece özeti salt-okunur
        Assert.DoesNotContain("delete_meal", toolNames);
        Assert.Contains("get_nutrition_history", toolNames);
        Assert.Contains(_app.Anthropic.SentTo(c1), t => t == "Koç cevabı");
        Assert.Contains("Yemekli", (string)single.Body!["system"]!);
    }

    [Fact]
    public async Task Link_code_endpoint_disabled_without_bot()
    {
        var app = new TestApp();
        try
        {
            var (web, _, _, _) = await app.RegisterAsync();
            Assert.False((await (await web.GetAsync("/api/account/telegram")).JsonAsync()).GetProperty("botEnabled").GetBoolean());
            Assert.Equal(HttpStatusCode.ServiceUnavailable, (await web.PostAsync("/api/account/telegram/link-code", null)).StatusCode);
        }
        finally { app.Dispose(); }
    }
}
