using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using FitTrack.API.Data;
using FitTrack.API.Models;
using FitTrack.API.Security;
using Microsoft.EntityFrameworkCore;

namespace FitTrack.API.Services;

/// <summary>
/// Telegram polling bot — ince sarmalayıcı, koçun beyni <see cref="CoachService"/>'te.
/// Her sohbet tek bir hesaba bağlıdır. Bağlama web'den alınan tek kullanımlık kodla yapılır
/// (<c>/link KOD</c>); bağlı olmayan sohbet hiçbir veriye dokunamaz.
/// Mesajlar sohbet başına sırayla, sohbetler arasında paralel işlenir; her iş kendi DI kapsamındadır.
/// </summary>
public class TelegramBotService : BackgroundService
{
    /// <summary>Tek kullanıcılı dönemin sahip sohbeti — yalnız eski veri sahiplenilirken okunur.</summary>
    public const string LegacyChatIdKey = "telegram.chatId";

    /// <summary>
    /// Ard arda kaç 409 Conflict'ten sonra yoklama tamamen bırakılır.
    /// Deploy sırasında eski konteyner bir süre daha ayakta kalabilir; birkaç
    /// çakışmaya tolerans gösterip sonra pes ediyoruz.
    /// </summary>
    private const int MaxConsecutiveConflicts = 6;

    private const int MaxLinkAttempts = 5;
    private static readonly TimeSpan LinkAttemptWindow = TimeSpan.FromMinutes(15);
    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // karışan karakterler yok

    internal const int MaxConcurrentCoachCalls = 4;
    internal const int MaxMessagesPerMinute = 10;
    internal const int MaxPendingPerChat = 20;
    private static readonly TimeSpan MinuteWindow = TimeSpan.FromMinutes(1);
    private const string TooFastWarning = "Çok hızlı yazıyorsun. Bir dakika bekleyip tekrar yaz.";
    private const string QueueFullWarning = "Bekleyen mesajın çok. Koç bunları bitirsin, sonra tekrar yaz.";

    private readonly IServiceScopeFactory _scope;
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _cfg;
    private readonly ILogger<TelegramBotService> _log;
    private readonly ConcurrentDictionary<long, (int Count, DateTime WindowStart)> _linkAttempts = new();

    private readonly SemaphoreSlim _coachSlots = new(MaxConcurrentCoachCalls);
    private readonly SemaphoreSlim _linkGate = new(1); // kod tek kullanımlık kalsın: sohbetler artık paralel
    private readonly object _queueLock = new();
    private readonly Dictionary<long, ChatQueue> _chats = new();
    private readonly HashSet<Task> _running = new();
    private DateTime _lastSweep = DateTime.MinValue;

    /// <summary>Dakikalık pencerelerin saati; testler ilerletir.</summary>
    internal Func<DateTime> Clock { get; set; } = () => DateTime.UtcNow;

    private sealed class ChatQueue
    {
        public readonly Queue<JsonNode?> Pending = new();
        public bool Running;
        public int InWindow;
        public DateTime WindowStart;
        public DateTime? LastWarnedAt;
    }

    public TelegramBotService(IServiceScopeFactory scope, IHttpClientFactory http, IConfiguration cfg, ILogger<TelegramBotService> log)
    {
        _scope = scope;
        _http = http;
        _cfg = cfg;
        _log = log;
    }

    public static bool IsEnabled(IConfiguration cfg) =>
        !string.IsNullOrEmpty(cfg["Telegram:BotToken"])
        && bool.TryParse(cfg["Telegram:Polling"], out var polling) && polling;

    public static string NewLinkCode() =>
        new(Enumerable.Range(0, 8).Select(_ => CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)]).ToArray());

    public static string HashLinkCode(string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code.Trim().ToUpperInvariant())));

    public static long? LegacyChatId(IConfiguration cfg, AppDbContext db)
    {
        if (long.TryParse(cfg["Telegram:ChatId"], out var fromCfg)) return fromCfg;
        var setting = db.AppSettings.AsNoTracking().FirstOrDefault(s => s.Key == LegacyChatIdKey);
        return long.TryParse(setting?.Value, out var fromDb) ? fromDb : null;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var token = _cfg["Telegram:BotToken"] ?? "";
        if (string.IsNullOrEmpty(token))
        {
            _log.LogWarning("Telegram bot token yok. Bot başlatılmadı.");
            return;
        }

        // Telegram'ın getUpdates'i tek tüketiciye izin verir. İki örnek aynı token'ı
        // yoklarsa Telegram her mesajı rastgele birine verir; ikisi de kendi
        // veritabanına bakıp cevap yazar ve kullanıcı çelişkili yanıtlar görür.
        // Bu yüzden yoklama açıkça izin verilmeden başlamaz — yerelde uygulamayı
        // çalıştırmak canlıdaki botu kaçırmaz.
        if (!IsEnabled(_cfg))
        {
            _log.LogInformation(
                "Telegram yoklaması kapalı (Telegram:Polling ayarlı değil). Bot başlatılmadı.");
            return;
        }

        // Acknowledge any pending updates so we start fresh.
        // Kuyruk bellekte: yeniden başlatmada bekleyenler de böylece düşer, hiçbir güncelleme iki kez işlenmez.
        try { await _http.CreateClient().GetAsync($"https://api.telegram.org/bot{token}/deleteWebhook?drop_pending_updates=true", ct); }
        catch { }

        _log.LogInformation("Telegram yoklaması başladı.");

        try { await PollAsync(token, ct); }
        finally
        {
            // Kapanışta süren işler iptal edilir, kuyruktakiler alınmaz. Kapsamlar kapanmadan önce hepsi bitsin.
            await WhenIdleAsync();
        }
    }

    private async Task PollAsync(string token, CancellationToken ct)
    {
        long lastId = 0;
        var conflicts = 0;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var c = _http.CreateClient();
                c.Timeout = TimeSpan.FromSeconds(35);
                var resp = await c.GetAsync(
                    $"https://api.telegram.org/bot{token}/getUpdates?timeout=30&offset={lastId + 1}", ct);
                var body = await resp.Content.ReadAsStringAsync(ct);

                var root = JsonNode.Parse(body);
                if ((bool?)root?["ok"] != true)
                {
                    // 409 = başka bir örnek aynı botu yokluyor. Sessizce yeniden
                    // denemek iki örneğin sonsuza kadar kapışması demek; bunu
                    // gürültüyle bildirip çekiliyoruz.
                    if ((int?)root?["error_code"] == 409)
                    {
                        conflicts++;
                        _log.LogWarning(
                            "Telegram 409 Conflict — başka bir örnek aynı botu yokluyor ({N}/{Max}). {Desc}",
                            conflicts, MaxConsecutiveConflicts, Scrub((string?)root?["description"], token));

                        if (conflicts >= MaxConsecutiveConflicts)
                        {
                            _log.LogError(
                                "Telegram yoklaması durduruldu: aynı token'ı kullanan başka bir örnek var. " +
                                "Aynı anda yalnızca tek bir örnek Telegram:Polling=true ile çalışmalı.");
                            return;
                        }

                        await Task.Delay(TimeSpan.FromSeconds(10), ct);
                        continue;
                    }

                    await Task.Delay(2000, ct);
                    continue;
                }

                conflicts = 0;

                var updates = root["result"]?.AsArray();
                if (updates is null) { await Task.Delay(1000, ct); continue; }

                foreach (var upd in updates)
                {
                    // Offset kuyruğa alınır alınmaz ilerler: bir sonraki yoklama bu güncellemeyi Telegram'a
                    // onaylar. Yoklama işi beklemez; aynı güncelleme ikinci kez gelirse işlenmez.
                    var updateId = (long?)upd?["update_id"] ?? 0;
                    if (updateId <= lastId) continue;
                    lastId = updateId;

                    try { Enqueue(token, upd, ct); }
                    catch (Exception ex) { _log.LogError("Error handling update {Id}: {Error}", updateId, Scrub(ex.ToString(), token)); }
                }
            }
            catch (TaskCanceledException) { break; }
            catch (Exception ex)
            {
                _log.LogWarning("Polling error: {Error}. Retrying in 5s...", Scrub(ex.ToString(), token));
                await Task.Delay(5000, ct);
            }
        }
    }

    /// <summary>
    /// Bot token'ı yalnız Telegram URL'sinde durur; istisna metni ya da hata açıklaması onu taşıyabilir.
    /// Log'a giden her metin buradan geçer.
    /// </summary>
    internal static string Scrub(string? text, string token)
    {
        if (string.IsNullOrEmpty(text)) return "";
        if (token.Length > 0) text = text.Replace(token, "***");
        return Regex.Replace(text, @"api\.telegram\.org/\S*", "api.telegram.org/***");
    }

    /// <summary>
    /// Güncellemeyi sohbetin kuyruğuna alır ve hemen döner. Sohbet başına dakikada
    /// <see cref="MaxMessagesPerMinute"/> mesaj ve <see cref="MaxPendingPerChat"/> bekleyen; aşılınca mesaj
    /// atılır, kullanıcıya dakikada en çok bir uyarı gider. Bağlantı işlendiği anda okunur, kuyruğa alındığında değil.
    /// </summary>
    internal void Enqueue(string token, JsonNode? upd, CancellationToken ct)
    {
        var msg = upd?["message"];
        var chatId = (long?)msg?["chat"]?["id"];
        if (chatId is null || string.IsNullOrWhiteSpace((string?)msg?["text"])) return;

        ChatQueue queue;
        string? warning = null;
        var start = false;
        lock (_queueLock)
        {
            var now = Clock();
            SweepIdleChats(now);
            if (!_chats.TryGetValue(chatId.Value, out queue!))
                _chats[chatId.Value] = queue = new ChatQueue { WindowStart = now };
            if (now - queue.WindowStart >= MinuteWindow)
            {
                queue.WindowStart = now;
                queue.InWindow = 0;
            }

            if (queue.InWindow >= MaxMessagesPerMinute) warning = TooFastWarning;
            else if (queue.Pending.Count >= MaxPendingPerChat) warning = QueueFullWarning;
            else
            {
                queue.InWindow++;
                queue.Pending.Enqueue(upd);
                if (!queue.Running) queue.Running = start = true;
            }

            if (warning is not null)
            {
                if (queue.LastWarnedAt is { } warned && now - warned < MinuteWindow) warning = null;
                else queue.LastWarnedAt = now;
            }
        }

        if (start) Track(Task.Run(() => DrainAsync(token, queue, ct), CancellationToken.None));
        if (warning is not null) Track(SendTelegram(token, chatId.Value, warning));
    }

    private async Task DrainAsync(string token, ChatQueue queue, CancellationToken ct)
    {
        while (true)
        {
            JsonNode? upd;
            lock (_queueLock)
            {
                if (ct.IsCancellationRequested) queue.Pending.Clear();
                if (queue.Pending.Count == 0)
                {
                    queue.Running = false;
                    return;
                }
                upd = queue.Pending.Dequeue();
            }

            try { await HandleUpdate(token, upd, ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { /* kapanış: döngü kuyruğu boşaltır */ }
            catch (Exception ex)
            {
                _log.LogError("Error handling update {Id}: {Error}", (long?)upd?["update_id"] ?? 0, Scrub(ex.ToString(), token));
            }
        }
    }

    private void SweepIdleChats(DateTime now)
    {
        if (now - _lastSweep < MinuteWindow) return;
        _lastSweep = now;
        foreach (var (id, q) in _chats)
        {
            var warningActive = q.LastWarnedAt is { } warned && now - warned < MinuteWindow;
            if (!q.Running && q.Pending.Count == 0 && now - q.WindowStart >= MinuteWindow && !warningActive)
                _chats.Remove(id);
        }
    }

    private void Track(Task task)
    {
        lock (_queueLock) _running.Add(task);
        task.ContinueWith(t => { lock (_queueLock) _running.Remove(t); }, TaskScheduler.Default);
    }

    /// <summary>Kuyruktaki ve süren tüm işler bitene kadar bekler.</summary>
    internal async Task WhenIdleAsync()
    {
        while (true)
        {
            Task[] running;
            lock (_queueLock) running = _running.ToArray();
            if (running.Length == 0) return;
            await Task.WhenAll(running);
            await Task.Yield(); // Track'in temizliği de çalışsın
        }
    }

    internal async Task HandleUpdate(string token, JsonNode? upd, CancellationToken ct)
    {
        var msg = upd?["message"];
        var text = ((string?)msg?["text"])?.Trim();
        var chatId = (long?)msg?["chat"]?["id"];
        var chatType = (string?)msg?["chat"]?["type"];

        if (string.IsNullOrEmpty(text) || chatId is null) return;

        // Grup sohbetinde herkes okur — sağlık verisi yalnız özel sohbette konuşulur.
        if (chatType != "private")
        {
            await SendTelegram(token, chatId.Value, "Koç yalnız özel sohbette çalışır.");
            return;
        }

        using var scope = _scope.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var user = await db.Users.FirstOrDefaultAsync(u => u.TelegramChatId == chatId, ct);

        var command = text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var verb = command[0].Split('@')[0].ToLowerInvariant(); // "/link@BotAdi" biçimi
        var arg = command.Length > 1 ? command[1].Trim() : "";

        if (verb is "/link" or "/start" && arg.Length > 0)
        {
            await LinkAsync(db, token, chatId.Value, arg, ct);
            return;
        }

        if (user is null)
        {
            await SendTelegram(token, chatId.Value,
                "Bu sohbet bir FitTrack hesabına bağlı değil. Web'de Hesap → Telegram bölümünden kod al, " +
                "sonra buraya /link KOD yaz.");
            return;
        }

        if (verb == "/unlink")
        {
            user.TelegramChatId = null;
            await db.SaveChangesAsync(ct);
            await SendTelegram(token, chatId.Value, "Bağlantı kaldırıldı. Bu sohbet artık hesabına erişemez.");
            return;
        }

        if (verb == "/start")
        {
            await SendTelegram(token, chatId.Value, "Koç hazır. Ne yediğini, kilonu, nasıl hissettiğini yaz — takip ederim. Soru da sorabilirsin: \"bu hafta nasıl gidiyor?\"");
            return;
        }
        if (text.StartsWith('/')) return;

        // Bu kapsamdaki her sorgu ve kayıt yalnız bu kullanıcıya ait.
        scope.ServiceProvider.GetRequiredService<CurrentUser>().ActAs(user.Id);
        var coach = scope.ServiceProvider.GetRequiredService<CoachService>();

        if (!await coach.HasKeyAsync(ct))
        {
            await SendTelegram(token, chatId.Value,
                "Koçu kullanmak için kendi Anthropic API anahtarını eklemen gerekiyor: web'de Hesap → AI anahtarı.");
            return;
        }

        // Typing indicator
        try { await _http.CreateClient().GetAsync($"https://api.telegram.org/bot{token}/sendChatAction?chat_id={chatId}&action=typing", ct); }
        catch (Exception ex) when (ex is not OperationCanceledException) { /* yalnız "yazıyor" göstergesi */ }

        if (text.Length > CoachService.MaxMessageChars) text = text[..CoachService.MaxMessageChars];

        // Save user message
        db.CoachMessages.Add(new CoachMessageRecord { Id = Guid.NewGuid(), Role = "user", Content = text, CreatedAt = DateTime.Now });
        await db.SaveChangesAsync(ct);

        // Son 3 günün geçmişiyle konuş.
        var since = DateTime.Now.Date.AddDays(-2);
        var history = await db.CoachMessages
            .Where(m => m.CreatedAt >= since)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(ct);

        var messages = new JsonArray();
        foreach (var h in history.TakeLast(30))
            messages.Add(new JsonObject { ["role"] = h.Role, ["content"] = h.Content });
        // TakeLast ilk mesajı assistant yapabilir; Anthropic konuşmanın user ile başlamasını ister.
        while (messages.Count > 0 && (string?)messages[0]?["role"] != "user") messages.RemoveAt(0);

        // Bir çağrı dakikalar sürebilir: yuva yalnız AI çağrısını sınırlar, diğer sohbetlerin kayıt ve komutlarını değil.
        CoachResult result;
        await _coachSlots.WaitAsync(ct);
        try { result = await coach.ChatAsync(messages, ct: ct); }
        finally { _coachSlots.Release(); }

        if (!result.Ok)
        {
            await SendTelegram(token, chatId.Value, result.KeyRejected
                ? "Anthropic anahtarını reddetti. Web'de Hesap → AI anahtarı bölümünden yenile."
                : "Koç şu an cevap veremedi.");
            return;
        }

        if (!string.IsNullOrWhiteSpace(result.Reply))
        {
            db.CoachMessages.Add(new CoachMessageRecord { Id = Guid.NewGuid(), Role = "assistant", Content = result.Reply, CreatedAt = DateTime.Now });
            await db.SaveChangesAsync(ct);
            await SendTelegram(token, chatId.Value, result.Reply);
        }
    }

    async Task LinkAsync(AppDbContext db, string token, long chatId, string code, CancellationToken ct)
    {
        // Kod 32^8 olasılık; yine de sohbet başına deneme sınırı var.
        var now = DateTime.UtcNow;
        var attempt = _linkAttempts.AddOrUpdate(chatId,
            _ => (1, now),
            (_, prev) => now - prev.WindowStart > LinkAttemptWindow ? (1, now) : (prev.Count + 1, prev.WindowStart));
        if (attempt.Count > MaxLinkAttempts)
        {
            await SendTelegram(token, chatId, "Çok fazla deneme. Biraz sonra tekrar dene.");
            return;
        }

        var hash = HashLinkCode(code);
        User? target;
        await _linkGate.WaitAsync(ct);
        try
        {
            target = await db.Users.FirstOrDefaultAsync(u =>
                u.TelegramLinkCodeHash == hash && u.TelegramLinkCodeExpiresAt != null && u.TelegramLinkCodeExpiresAt > now, ct);
            if (target is not null)
            {
                // Bu sohbet başka hesaba bağlıysa önce oradan çöz — bir sohbet tek hesaba bakar.
                var previous = await db.Users.Where(u => u.TelegramChatId == chatId && u.Id != target.Id).ToListAsync(ct);
                foreach (var p in previous) p.TelegramChatId = null;
                await db.SaveChangesAsync(ct);

                target.TelegramChatId = chatId;
                target.TelegramLinkCodeHash = null; // tek kullanımlık
                target.TelegramLinkCodeExpiresAt = null;
                await db.SaveChangesAsync(ct);
            }
        }
        finally { _linkGate.Release(); }

        if (target is null)
        {
            await SendTelegram(token, chatId, "Kod geçersiz ya da süresi dolmuş. Web'den yeni kod al.");
            return;
        }
        _linkAttempts.TryRemove(chatId, out _);

        _log.LogInformation("Telegram sohbeti hesaba bağlandı: {UserId}", target.Id);
        await SendTelegram(token, chatId, $"Bağlandı, {target.DisplayName}. Artık buradan da yazabilirsin. Kaldırmak için /unlink.");
    }

    private Task SendTelegram(string token, long chat, string text) => SendTelegram(_http, token, chat, text);

    public static async Task SendTelegram(IHttpClientFactory factory, string token, long chat, string text)
    {
        try
        {
            if (text.Length > 4000) text = text[..4000];
            var http = factory.CreateClient();
            var body = new JsonObject { ["chat_id"] = chat, ["text"] = text }.ToJsonString();
            await http.PostAsync(
                $"https://api.telegram.org/bot{token}/sendMessage",
                new StringContent(body, Encoding.UTF8, "application/json"));
        }
        catch { /* logged by caller context if needed */ }
    }
}
