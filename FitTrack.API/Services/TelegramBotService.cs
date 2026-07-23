using System.Text;
using System.Text.Json.Nodes;
using FitTrack.API.Data;
using FitTrack.API.Models;
using Microsoft.EntityFrameworkCore;

namespace FitTrack.API.Services;

/// <summary>
/// Telegram polling bot — ince sarmalayıcı, koçun beyni <see cref="CoachService"/>'te.
/// Gelen chat id'yi AppSettings'e kaydeder ki proaktif servis oradan yazabilsin.
/// </summary>
public class TelegramBotService : BackgroundService
{
    public const string ChatIdKey = "telegram.chatId";

    private readonly IServiceScopeFactory _scope;
    private readonly IConfiguration _cfg;
    private readonly ILogger<TelegramBotService> _log;

    public TelegramBotService(IServiceScopeFactory scope, IConfiguration cfg, ILogger<TelegramBotService> log)
    {
        _scope = scope;
        _cfg = cfg;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var token = _cfg["Telegram:BotToken"] ?? "";
        if (string.IsNullOrEmpty(token))
        {
            _log.LogWarning("Telegram bot token not configured. Bot will not start.");
            return;
        }

        // Acknowledge any pending updates so we start fresh.
        using (var http = new HttpClient())
        {
            try { await http.GetAsync($"https://api.telegram.org/bot{token}/deleteWebhook?drop_pending_updates=true", ct); }
            catch { }
        }

        long lastId = 0;
        _log.LogInformation("Telegram bot polling started.");

        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var c = new HttpClient { Timeout = TimeSpan.FromSeconds(35) };
                var resp = await c.GetAsync(
                    $"https://api.telegram.org/bot{token}/getUpdates?timeout=30&offset={lastId + 1}", ct);
                var body = await resp.Content.ReadAsStringAsync(ct);

                var root = JsonNode.Parse(body);
                if ((bool?)root?["ok"] != true) { await Task.Delay(2000, ct); continue; }

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

    async Task HandleUpdate(string token, JsonNode? upd, CancellationToken ct)
    {
        var msg = upd?["message"];
        var text = (string?)msg?["text"];
        var chatId = (long?)msg?["chat"]?["id"];

        if (string.IsNullOrEmpty(text) || chatId is null) return;

        using var scope = _scope.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Chat id'yi kaydet — proaktif mesajlar için.
        await SaveChatIdAsync(db, chatId.Value);

        if (text.StartsWith("/start"))
        {
            await SendTelegram(token, chatId.Value, "Koç hazır. Ne yediğini, kilonu, nasıl hissettiğini yaz — takip ederim. Soru da sorabilirsin: \"bu hafta nasıl gidiyor?\"");
            return;
        }
        if (text.StartsWith("/")) return;

        // Typing indicator
        using (var http = new HttpClient())
            await http.GetAsync($"https://api.telegram.org/bot{token}/sendChatAction?chat_id={chatId}&action=typing", ct);

        var coach = scope.ServiceProvider.GetRequiredService<CoachService>();

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

        var result = await coach.ChatAsync(messages, ct: ct);
        if (!result.Ok)
        {
            await SendTelegram(token, chatId.Value, "Koç şu an cevap veremedi.");
            return;
        }

        if (!string.IsNullOrWhiteSpace(result.Reply))
        {
            db.CoachMessages.Add(new CoachMessageRecord { Id = Guid.NewGuid(), Role = "assistant", Content = result.Reply, CreatedAt = DateTime.Now });
            await db.SaveChangesAsync(ct);
            await SendTelegram(token, chatId.Value, result.Reply);
        }
    }

    static async Task SaveChatIdAsync(AppDbContext db, long chatId)
    {
        var setting = await db.AppSettings.FindAsync(ChatIdKey);
        var val = chatId.ToString();
        if (setting is null)
            db.AppSettings.Add(new AppSetting { Key = ChatIdKey, Value = val });
        else if (setting.Value != val)
            setting.Value = val;
        else return;
        await db.SaveChangesAsync();
    }

    public static async Task SendTelegram(string token, long chat, string text)
    {
        try
        {
            if (text.Length > 4000) text = text[..4000];
            using var http = new HttpClient();
            var body = new JsonObject { ["chat_id"] = chat, ["text"] = text }.ToJsonString();
            await http.PostAsync(
                $"https://api.telegram.org/bot{token}/sendMessage",
                new StringContent(body, Encoding.UTF8, "application/json"));
        }
        catch { /* logged by caller context if needed */ }
    }
}
