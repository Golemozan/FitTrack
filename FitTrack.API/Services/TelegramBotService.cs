using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using FitTrack.API.Data;
using FitTrack.API.Models;
using FitTrack.API.Security;
using Microsoft.EntityFrameworkCore;

namespace FitTrack.API.Services;

/// <summary>
/// Telegram polling bot — ince sarmalayıcı, koçun beyni <see cref="CoachService"/>'te.
/// Her sohbet tek bir hesaba bağlıdır. Bağlama web'den alınan tek kullanımlık kodla yapılır
/// (<c>/link KOD</c>); bağlı olmayan sohbet hiçbir veriye dokunamaz.
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

    private readonly IServiceScopeFactory _scope;
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _cfg;
    private readonly ILogger<TelegramBotService> _log;
    private readonly ConcurrentDictionary<long, (int Count, DateTime WindowStart)> _linkAttempts = new();

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
        try { await _http.CreateClient().GetAsync($"https://api.telegram.org/bot{token}/deleteWebhook?drop_pending_updates=true", ct); }
        catch { }

        long lastId = 0;
        var conflicts = 0;
        _log.LogInformation("Telegram yoklaması başladı.");

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
                            conflicts, MaxConsecutiveConflicts, (string?)root?["description"]);

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
                    var updateId = (long?)upd?["update_id"] ?? 0;
                    if (updateId > lastId) lastId = updateId;

                    try { await HandleUpdate(token, upd, ct); }
                    catch (Exception ex) { _log.LogError(ex, "Error handling update {Id}", updateId); }
                }
            }
            catch (TaskCanceledException) { break; }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Polling error. Retrying in 5s...");
                await Task.Delay(5000, ct);
            }
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

        var result = await coach.ChatAsync(messages, ct: ct);
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
        var target = await db.Users.FirstOrDefaultAsync(u =>
            u.TelegramLinkCodeHash == hash && u.TelegramLinkCodeExpiresAt != null && u.TelegramLinkCodeExpiresAt > now, ct);
        if (target is null)
        {
            await SendTelegram(token, chatId, "Kod geçersiz ya da süresi dolmuş. Web'den yeni kod al.");
            return;
        }

        // Bu sohbet başka hesaba bağlıysa önce oradan çöz — bir sohbet tek hesaba bakar.
        var previous = await db.Users.Where(u => u.TelegramChatId == chatId && u.Id != target.Id).ToListAsync(ct);
        foreach (var p in previous) p.TelegramChatId = null;
        await db.SaveChangesAsync(ct);

        target.TelegramChatId = chatId;
        target.TelegramLinkCodeHash = null; // tek kullanımlık
        target.TelegramLinkCodeExpiresAt = null;
        await db.SaveChangesAsync(ct);
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
