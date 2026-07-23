using System.Text.Json.Nodes;
using FitTrack.API.Data;
using FitTrack.API.Models;
using Microsoft.EntityFrameworkCore;

namespace FitTrack.API.Services;

/// <summary>
/// Proaktif koç: veriyi kendiliğinden okur, Telegram'dan yazar.
/// - 14:00 — bugün hiç öğün kaydı yoksa dürtme.
/// - 21:00 — günün özeti (Claude ile üretilir, tool erişimi var).
/// Gönderim tarihleri AppSettings'te tutulur; restart'ta mükerrer mesaj atmaz.
/// </summary>
public class DailyCoachService : BackgroundService
{
    private const string SummaryDateKey = "coach.lastSummaryDate";
    private const string NudgeDateKey = "coach.lastNudgeDate";
    private const int NudgeHour = 14;
    private const int SummaryHour = 21;

    private readonly IServiceScopeFactory _scope;
    private readonly IConfiguration _cfg;
    private readonly ILogger<DailyCoachService> _log;

    public DailyCoachService(IServiceScopeFactory scope, IConfiguration cfg, ILogger<DailyCoachService> log)
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
            _log.LogWarning("Telegram token yok — proaktif koç kapalı.");
            return;
        }

        _log.LogInformation("Proaktif koç başladı (dürtme {N}:00, özet {S}:00).", NudgeHour, SummaryHour);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.Now;
                if (now.Hour >= NudgeHour) await TryNudgeAsync(token, ct);
                if (now.Hour >= SummaryHour) await TrySummaryAsync(token, ct);
            }
            catch (Exception ex) { _log.LogError(ex, "Proaktif koç hatası."); }

            await Task.Delay(TimeSpan.FromMinutes(5), ct);
        }
    }

    async Task TryNudgeAsync(string token, CancellationToken ct)
    {
        using var scope = _scope.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var today = DateTime.Now.Date;
        if (await AlreadySentAsync(db, NudgeDateKey, today)) return;

        var chatId = await GetChatIdAsync(db);
        if (chatId is null) return;

        var hasMeals = await db.MealEntries.AnyAsync(m => m.LoggedAt >= today && m.LoggedAt < today.AddDays(1), ct);
        await MarkSentAsync(db, NudgeDateKey, today); // kayıt varsa da işaretle — bugün bir daha bakma
        if (hasMeals) return;

        await TelegramBotService.SendTelegram(token, chatId.Value,
            $"Saat {DateTime.Now:HH:mm}, bugün hiç öğün kaydı yok. Ne yediysen yaz, kaydedeyim.");
        _log.LogInformation("Dürtme gönderildi.");
    }

    async Task TrySummaryAsync(string token, CancellationToken ct)
    {
        using var scope = _scope.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var today = DateTime.Now.Date;
        if (await AlreadySentAsync(db, SummaryDateKey, today)) return;

        var chatId = await GetChatIdAsync(db);
        if (chatId is null) return;

        // Hiç veri yoksa özet üretme (dürtme zaten gitti).
        var hasAny = await db.MealEntries.AnyAsync(m => m.LoggedAt >= today, ct)
                  || await db.WeightLogs.AnyAsync(w => w.LoggedAt >= today, ct)
                  || await db.WorkoutSessions.AnyAsync(w => w.LoggedAt >= today, ct);
        await MarkSentAsync(db, SummaryDateKey, today);
        if (!hasAny) return;

        var coach = scope.ServiceProvider.GetRequiredService<CoachService>();
        var messages = new JsonArray
        {
            new JsonObject
            {
                ["role"] = "user",
                ["content"] = "Günü kapat: bugünün kısa özetini çıkar. Kalori/makro vs hedef, antrenman varsa hacim, dikkat çeken 1-2 gözlem, yarın için tek somut öneri. Maksimum 6-7 satır. Gerekirse geçmiş tool'larına bak.",
            },
        };

        var result = await coach.ChatAsync(messages,
            extraSystem: "Bu otomatik gece özeti — Ozan mesaj atmadı, sen kendiliğinden yazıyorsun. Veri kaydetme tool'larını KULLANMA, sadece okuma.",
            ct: ct);

        if (result.Ok && !string.IsNullOrWhiteSpace(result.Reply))
        {
            db.CoachMessages.Add(new CoachMessageRecord { Id = Guid.NewGuid(), Role = "assistant", Content = result.Reply, CreatedAt = DateTime.Now });
            await db.SaveChangesAsync(ct);
            await TelegramBotService.SendTelegram(token, chatId.Value, result.Reply);
            _log.LogInformation("Gece özeti gönderildi.");
        }
    }

    async Task<long?> GetChatIdAsync(AppDbContext db)
    {
        var cfgId = _cfg["Telegram:ChatId"];
        if (long.TryParse(cfgId, out var fromCfg)) return fromCfg;
        var setting = await db.AppSettings.FindAsync(TelegramBotService.ChatIdKey);
        return long.TryParse(setting?.Value, out var fromDb) ? fromDb : null;
    }

    static async Task<bool> AlreadySentAsync(AppDbContext db, string key, DateTime day)
    {
        var setting = await db.AppSettings.FindAsync(key);
        return setting?.Value == day.ToString("yyyy-MM-dd");
    }

    static async Task MarkSentAsync(AppDbContext db, string key, DateTime day)
    {
        var val = day.ToString("yyyy-MM-dd");
        var setting = await db.AppSettings.FindAsync(key);
        if (setting is null) db.AppSettings.Add(new AppSetting { Key = key, Value = val });
        else setting.Value = val;
        await db.SaveChangesAsync();
    }
}
