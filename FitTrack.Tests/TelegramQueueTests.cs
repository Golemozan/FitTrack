using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FitTrack.API.Services;
using FitTrack.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;

namespace FitTrack.Tests;

/// <summary>
/// Bot kuyruğu: sohbet başına sıralı, sohbetler arasında paralel, koç çağrısı sınırlı, kota ve bekleme tavanı var.
/// Telegram ve Anthropic sahte; her test kendi bot örneğini kullanır (canlıda da tek örnek var).
/// </summary>
public class TelegramQueueTests : IClassFixture<TelegramApp>
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(20);

    private readonly TelegramApp _app;
    private readonly TelegramBotService _bot;

    public TelegramQueueTests(TelegramApp app)
    {
        _app = app;
        _bot = app.TelegramBot();
        _app.Anthropic.BeforeMessage = null;
        _app.Anthropic.GetUpdates = null;
    }

    private static long NewChat() => Random.Shared.NextInt64(1_000_000, long.MaxValue / 2);

    private static JsonObject Update(long chat, string text, long updateId = 1) => new()
    {
        ["update_id"] = updateId,
        ["message"] = new JsonObject
        {
            ["text"] = text,
            ["chat"] = new JsonObject { ["id"] = chat, ["type"] = "private" },
        },
    };

    private void Post(long chat, string text) => _bot.Enqueue(TestApp.TelegramToken, Update(chat, text), CancellationToken.None);

    /// <summary>Anahtarı olan, sohbete bağlı bir hesap.</summary>
    private async Task<(HttpClient Web, long Chat)> LinkedChat()
    {
        var (web, _, _, _) = await _app.RegisterAsync("Kuyruk");
        var chat = NewChat();
        var code = (await (await web.PostAsync("/api/account/telegram/link-code", null)).JsonAsync()).GetProperty("code").GetString()!;
        await _bot.HandleUpdate(TestApp.TelegramToken, Update(chat, $"/link {code}"), CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, (await web.PutAsJsonAsync("/api/account/ai-key", new { key = FakeAnthropic.KeyA })).StatusCode);
        return (web, chat);
    }

    private static async Task Eventually(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Patience;
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "Beklenen durum zamanında oluşmadı.");
            await Task.Delay(10);
        }
    }

    [Fact]
    public async Task Same_chat_is_sequential_and_keeps_order()
    {
        var (web, chat) = await LinkedChat();
        int current = 0, max = 0;
        _app.Anthropic.BeforeMessage = async ct =>
        {
            var now = Interlocked.Increment(ref current);
            InterlockedMax(ref max, now);
            await Task.Delay(50, ct);
            Interlocked.Decrement(ref current);
        };

        foreach (var text in new[] { "sira-bir", "sira-iki", "sira-uc" }) Post(chat, text);
        await _bot.WhenIdleAsync();

        Assert.Equal(1, max);
        Assert.Equal(3, _app.Anthropic.SentTo(chat).Count(t => t == "Koç cevabı"));
        var history = await web.GetStringAsync("/api/coach/history");
        var positions = new[] { "sira-bir", "sira-iki", "sira-uc" }.Select(t => history.IndexOf(t, StringComparison.Ordinal)).ToList();
        Assert.DoesNotContain(-1, positions);
        Assert.Equal(positions.OrderBy(p => p), positions);
    }

    /// <summary>Beş sohbet aynı anda koça gider: dördü girer, beşincisi bekler. Bu sırada koça gitmeyen sohbet gecikmez.</summary>
    [Fact]
    public async Task Coach_calls_are_capped_and_other_chats_keep_flowing()
    {
        var chats = new List<long>();
        for (var i = 0; i < 5; i++) chats.Add((await LinkedChat()).Chat);

        var entered = 0;
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _app.Anthropic.BeforeMessage = async _ =>
        {
            Interlocked.Increment(ref entered);
            await release.Task;
        };

        foreach (var chat in chats) Post(chat, "koca-soru");
        await Eventually(() => Volatile.Read(ref entered) == TelegramBotService.MaxConcurrentCoachCalls);

        // Tüm koç yuvaları dolu, tek kuyrukta olsaydı bu cevap gelmezdi.
        var unlinked = NewChat();
        Post(unlinked, "merhaba");
        await Eventually(() => _app.Anthropic.SentTo(unlinked).Any(t => t.Contains("bağlı değil")));

        await Task.Delay(200);
        Assert.Equal(TelegramBotService.MaxConcurrentCoachCalls, Volatile.Read(ref entered));

        release.SetResult();
        await _bot.WhenIdleAsync();
        Assert.Equal(chats.Count, Volatile.Read(ref entered));
        Assert.All(chats, chat => Assert.Contains("Koç cevabı", _app.Anthropic.SentTo(chat)));
    }

    [Fact]
    public async Task Rate_limit_drops_excess_and_warns_once_a_minute()
    {
        var now = DateTime.UtcNow;
        _bot.Clock = () => now;
        var chat = NewChat();

        for (var i = 0; i < TelegramBotService.MaxMessagesPerMinute + 5; i++) Post(chat, $"m{i}");
        await _bot.WhenIdleAsync();
        Assert.Equal(TelegramBotService.MaxMessagesPerMinute, _app.Anthropic.SentTo(chat).Count(t => t.Contains("bağlı değil")));
        Assert.Single(_app.Anthropic.SentTo(chat), t => t.Contains("Çok hızlı"));

        // Yeni dakika: kota yenilenir, aşılırsa uyarı bir kez daha gider.
        now = now.AddSeconds(61);
        for (var i = 0; i < TelegramBotService.MaxMessagesPerMinute + 2; i++) Post(chat, $"n{i}");
        await _bot.WhenIdleAsync();
        Assert.Equal(TelegramBotService.MaxMessagesPerMinute * 2, _app.Anthropic.SentTo(chat).Count(t => t.Contains("bağlı değil")));
        Assert.Equal(2, _app.Anthropic.SentTo(chat).Count(t => t.Contains("Çok hızlı")));

        // Başka sohbet etkilenmez.
        var other = NewChat();
        Post(other, "selam");
        await _bot.WhenIdleAsync();
        Assert.Single(_app.Anthropic.SentTo(other), t => t.Contains("bağlı değil"));
    }

    [Fact]
    public async Task Pending_queue_is_capped_per_chat()
    {
        var (_, chat) = await LinkedChat();
        var now = DateTime.UtcNow;
        _bot.Clock = () => now;

        var entered = 0;
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _app.Anthropic.BeforeMessage = async _ =>
        {
            Interlocked.Increment(ref entered);
            await release.Task;
        };

        Post(chat, "ilk");
        await Eventually(() => Volatile.Read(ref entered) == 1); // ilk mesaj koçta, kuyruk boş

        for (var minute = 1; minute <= 3; minute++)
        {
            now = now.AddSeconds(61);
            for (var i = 0; i < TelegramBotService.MaxMessagesPerMinute; i++) Post(chat, $"d{minute}-{i}");
        }
        await Eventually(() => _app.Anthropic.SentTo(chat).Any(t => t.Contains("Bekleyen mesajın çok")));
        Assert.Single(_app.Anthropic.SentTo(chat), t => t.Contains("Bekleyen mesajın çok"));

        release.SetResult();
        await _bot.WhenIdleAsync();
        Assert.Equal(1 + TelegramBotService.MaxPendingPerChat, _app.Anthropic.SentTo(chat).Count(t => t == "Koç cevabı"));
    }

    /// <summary>Bağlantı işlendiği anda okunur: kuyrukta beklerken parola değişirse mesaj koça gitmez.</summary>
    [Fact]
    public async Task Queued_message_respects_a_link_dropped_while_waiting()
    {
        var (web, _, _, password) = await _app.RegisterAsync("Bekleyen");
        var chat = NewChat();
        var code = (await (await web.PostAsync("/api/account/telegram/link-code", null)).JsonAsync()).GetProperty("code").GetString()!;
        await _bot.HandleUpdate(TestApp.TelegramToken, Update(chat, $"/link {code}"), CancellationToken.None);
        await web.PutAsJsonAsync("/api/account/ai-key", new { key = FakeAnthropic.KeyA });

        var entered = 0;
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _app.Anthropic.BeforeMessage = async _ =>
        {
            Interlocked.Increment(ref entered);
            await release.Task;
        };
        Post(chat, "birinci");
        await Eventually(() => Volatile.Read(ref entered) == 1);
        Post(chat, "ikinci");

        await web.PostAsJsonAsync("/api/account/password", new { currentPassword = password, newPassword = "bekleyen-yeni-parola-3" });
        release.SetResult();
        await _bot.WhenIdleAsync();

        Assert.Equal(1, Volatile.Read(ref entered));
        Assert.Contains(_app.Anthropic.SentTo(chat), t => t.Contains("bağlı değil"));
    }

    [Fact]
    public async Task Polling_advances_offset_and_handles_each_update_once()
    {
        var chat = NewChat();
        var offsets = new ConcurrentQueue<long>();
        var batches = new ConcurrentQueue<Func<JsonArray>>();
        batches.Enqueue(() => new JsonArray(Update(chat, "bir", 5), Update(chat, "iki", 6)));
        batches.Enqueue(() => new JsonArray(Update(chat, "iki", 6))); // Telegram aynı güncellemeyi yeniden verse bile
        batches.Enqueue(() => new JsonArray(Update(chat, "uc", 7)));
        _app.Anthropic.GetUpdates = async (request, ct) =>
        {
            var query = request.RequestUri!.Query;
            offsets.Enqueue(long.Parse(query[(query.IndexOf("offset=", StringComparison.Ordinal) + 7)..]));
            if (batches.TryDequeue(out var next)) return next();
            await Task.Delay(10, ct);
            return new JsonArray();
        };

        await _bot.StartAsync(CancellationToken.None);
        await Eventually(() => offsets.Count >= 4 && _app.Anthropic.SentTo(chat).Count() >= 3);
        await _bot.WhenIdleAsync(); // yinelenen güncelleme işlendiyse cevabı burada görünür
        await _bot.StopAsync(new CancellationTokenSource(Patience).Token);

        Assert.Equal(new long[] { 1, 7, 7, 8 }, offsets.Take(4));
        Assert.Equal(3, _app.Anthropic.SentTo(chat).Count(t => t.Contains("bağlı değil")));
    }

    /// <summary>Kapanışta süren çağrı iptal edilir, kuyruktaki alınmaz ve servis temiz biter.</summary>
    [Fact]
    public async Task Shutdown_cancels_running_work_and_drops_the_queue()
    {
        var (_, chat) = await LinkedChat();
        var entered = 0;
        _app.Anthropic.BeforeMessage = async ct =>
        {
            Interlocked.Increment(ref entered);
            await Task.Delay(Timeout.Infinite, ct);
        };
        var sent = 0;
        _app.Anthropic.GetUpdates = async (_, ct) =>
        {
            if (Interlocked.Exchange(ref sent, 1) == 0)
                return new JsonArray(Update(chat, "uzun-is", 10), Update(chat, "bekleyen-is", 11));
            await Task.Delay(10, ct);
            return new JsonArray();
        };

        await _bot.StartAsync(CancellationToken.None);
        await Eventually(() => Volatile.Read(ref entered) == 1);
        await _bot.StopAsync(new CancellationTokenSource(Patience).Token);

        Assert.True(_bot.ExecuteTask!.IsCompleted, "Servis kapanışta askıda kaldı.");
        Assert.True(_bot.WhenIdleAsync().IsCompleted, "Servis, süren iş bitmeden kapandı: kapsamlar altından çekilirdi.");
        Assert.Equal(1, Volatile.Read(ref entered));
        Assert.DoesNotContain("Koç cevabı", _app.Anthropic.SentTo(chat));
    }

    /// <summary>Aynı kodu iki sohbet aynı anda yollasa da kod tek kullanımlık kalır.</summary>
    [Fact]
    public async Task Link_code_stays_single_use_across_parallel_chats()
    {
        var (web, _, _, _) = await _app.RegisterAsync("Yaris");
        var code = (await (await web.PostAsync("/api/account/telegram/link-code", null)).JsonAsync()).GetProperty("code").GetString()!;
        var chats = Enumerable.Range(0, 6).Select(_ => NewChat()).ToList();

        foreach (var chat in chats) Post(chat, $"/link {code}");
        await _bot.WhenIdleAsync();

        Assert.Single(chats, chat => _app.Anthropic.SentTo(chat).Any(t => t.StartsWith("Bağlandı")));
        Assert.Equal(chats.Count - 1, chats.Count(chat => _app.Anthropic.SentTo(chat).Any(t => t.Contains("geçersiz"))));
    }

    /// <summary>Paralel işlemenin yan etkisi: okuyan varken yazan commit'te "database is locked" ile düşmemeli.</summary>
    [Fact]
    public async Task Write_waits_for_a_concurrent_reader_instead_of_failing()
    {
        var (web, _, _, _) = await _app.RegisterAsync("Kilit");
        var meal = new { foodName = "x", grams = 1, calories = 1, protein = 1, carbs = 1, fat = 1, mealType = "Lunch" };
        var mealId = (await (await web.PostAsJsonAsync("/api/nutrition/log", meal)).JsonAsync()).GetProperty("id").GetString();

        using var reader = _app.OpenDb();
        using (var begin = reader.CreateCommand())
        {
            begin.CommandText = "BEGIN; SELECT COUNT(*) FROM Users;";
            begin.ExecuteScalar();
        }

        // UPDATE ... RETURNING: commit, kütüphanenin yeniden denemesinin dışında kalan adımda olur.
        var write = web.PutAsJsonAsync($"/api/nutrition/log/{mealId}", meal with { calories = 2 });
        await Task.Delay(400); // yazı commit'te okuyanı bekliyor
        using (var commit = reader.CreateCommand())
        {
            commit.CommandText = "COMMIT;";
            commit.ExecuteNonQuery();
        }

        Assert.Equal(HttpStatusCode.OK, (await write).StatusCode);
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int seen;
        while (value > (seen = Volatile.Read(ref target)))
            if (Interlocked.CompareExchange(ref target, value, seen) == seen) return;
    }
}

/// <summary>
/// Ortam ayarı HttpClient günlüğünü en geniş biçimde açmaya çalışır (kategori, alt kategori, sağlayıcı).
/// Token yine hiçbir satırda geçmemeli.
/// </summary>
public class TelegramLogApp : TelegramApp
{
    public const string SecretToken = "424242:sahte-gizli-token-degeri";
    protected override bool CaptureLogs => true;

    protected override void ExtraSettings(IWebHostBuilder builder)
    {
        builder.UseSetting("Telegram:BotToken", SecretToken);
        builder.UseSetting("Logging:LogLevel:Default", "Trace");
        builder.UseSetting("Logging:LogLevel:System.Net.Http.HttpClient", "Trace");
        builder.UseSetting("Logging:LogLevel:System.Net.Http.HttpClient.Default.LogicalHandler", "Trace");
        builder.UseSetting("Logging:LogLevel:System.Net.Http.HttpClient.Default.ClientHandler", "Trace");
    }
}

public class TelegramLogTests : IClassFixture<TelegramLogApp>
{
    private const string Token = TelegramLogApp.SecretToken;
    private readonly TelegramLogApp _app;

    public TelegramLogTests(TelegramLogApp app)
    {
        _app = app;
        _app.Anthropic.GetUpdates = null;
    }

    private static JsonObject Update(long chat, string text, long updateId = 1) => new()
    {
        ["update_id"] = updateId,
        ["message"] = new JsonObject { ["text"] = text, ["chat"] = new JsonObject { ["id"] = chat, ["type"] = "private" } },
    };

    private void AssertNoTokenInLogs()
    {
        var lines = _app.Logs.Lines.ToList();
        Assert.NotEmpty(lines); // yakalama gerçekten çalışıyor
        Assert.DoesNotContain(lines, l => l.Message.Contains(Token) || l.Message.Contains("sahte-gizli"));
    }

    [Fact]
    public async Task Successful_telegram_calls_never_log_the_token()
    {
        var bot = _app.TelegramBot();
        var unlinkedChat = Random.Shared.NextInt64(1_000_000, long.MaxValue / 2);
        var batches = new ConcurrentQueue<JsonArray>();
        batches.Enqueue(new JsonArray(Update(unlinkedChat, "merhaba", 3)));
        _app.Anthropic.GetUpdates = async (_, ct) =>
        {
            if (batches.TryDequeue(out var next)) return next;
            await Task.Delay(10, ct);
            return new JsonArray();
        };

        await bot.StartAsync(CancellationToken.None);
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (!_app.Anthropic.SentTo(unlinkedChat).Any() && DateTime.UtcNow < deadline) await Task.Delay(10);
        await bot.StopAsync(new CancellationTokenSource(TimeSpan.FromSeconds(20)).Token);

        Assert.NotEmpty(_app.Anthropic.SentTo(unlinkedChat)); // token'lı URL'ye gerçekten istek gitti
        AssertNoTokenInLogs();
    }

    /// <summary>İstisna metni tam URL'yi taşısa bile log'a token düşmez.</summary>
    [Fact]
    public async Task Polling_error_with_url_in_exception_does_not_log_the_token()
    {
        var bot = _app.TelegramBot();
        var failed = 0;
        _app.Anthropic.GetUpdates = (request, _) =>
        {
            Interlocked.Increment(ref failed);
            throw new HttpRequestException($"Bağlantı kurulamadı: {request.RequestUri}");
        };

        await bot.StartAsync(CancellationToken.None);
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (!_app.Logs.Lines.Any(l => l.Message.Contains("Polling error")) && DateTime.UtcNow < deadline) await Task.Delay(10);
        await bot.StopAsync(new CancellationTokenSource(TimeSpan.FromSeconds(20)).Token);

        Assert.True(failed > 0);
        Assert.Contains(_app.Logs.Lines, l => l.Message.Contains("Polling error"));
        AssertNoTokenInLogs();
    }

    [Fact]
    public void Scrub_removes_token_and_telegram_urls()
    {
        var text = $"GET https://api.telegram.org/bot{Token}/getUpdates?offset=1 başarısız; token={Token}";
        var clean = TelegramBotService.Scrub(text, Token);
        Assert.DoesNotContain(Token, clean);
        Assert.DoesNotContain("getUpdates", clean);
        Assert.Contains("başarısız", clean);
        // Token bilinmese de Telegram URL'si maskelenir.
        Assert.DoesNotContain("gizli", TelegramBotService.Scrub($"https://api.telegram.org/bot{Token}/x", ""));
        Assert.Equal("", TelegramBotService.Scrub(null, Token));
    }
}
