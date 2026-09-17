using System.Text.Json.Nodes;
using FitTrack.API.Data;
using FitTrack.API.Models;
using FitTrack.API.Security;
using Microsoft.EntityFrameworkCore;

namespace FitTrack.API.Services;

/// <summary>
/// Proaktif koç: Telegram'a bağlı her hesabın verisini kendiliğinden okur, Telegram'dan yazar.
/// - 14:00 — bugün hiç öğün kaydı yoksa dürtme (AI gerekmez).
/// - 21:00 — günün özeti (Claude ile üretilir; yalnız kendi anahtarı olan hesaplara).
/// Her hesap kendi kapsamında işlenir: bir kullanıcının sorgusu başkasının verisini göremez.
/// Gönderim tarihleri AppSettings'te kullanıcı başına tutulur; restart'ta mükerrer mesaj atmaz.
/// </summary>
public class DailyCoachService : BackgroundService
{
    private const string SummaryDateKey = "coach.lastSummaryDate";
    private const string NudgeDateKey = "coach.lastNudgeDate";
    private const int NudgeHour = 14;
    private const int SummaryHour = 21;

    private readonly IServiceScopeFactory _scope;
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _cfg;
    private readonly ILogger<DailyCoachService> _log;

    public DailyCoachService(IServiceScopeFactory scope, IHttpClientFactory http, IConfiguration cfg, ILogger<DailyCoachService> log)
    {
        _scope = scope;
        _http = http;
        _cfg = cfg;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var token = _cfg["Telegram:BotToken"] ?? "";
        if (!TelegramBotService.IsEnabled(_cfg))
        {
            _log.LogWarning("Telegram kapalı — proaktif koç kapalı.");
            return;
        }

        _log.LogInformation("Proaktif koç başladı (dürtme {N}:00, özet {S}:00).", NudgeHour, SummaryHour);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.Now;
                if (now.Hour >= NudgeHour)
                {
                    foreach (var (userId, chatId) in await LinkedUsersAsync(ct))
                    {
                        try
                        {
                            await TryNudgeAsync(token, userId, chatId, ct);
                            if (now.Hour >= SummaryHour) await TrySummaryAsync(token, userId, chatId, ct);
                        }
                        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                        catch (Exception ex) { _log.LogError(ex, "Proaktif koç hatası ({UserId}).", userId); }
                    }
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex) { _log.LogError(ex, "Proaktif koç hatası."); }

            await Task.Delay(TimeSpan.FromMinutes(5), ct);
        }
    }

    internal async Task<List<(Guid, long)>> LinkedUsersAsync(CancellationToken ct)
    {
        using var scope = _scope.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rows = await db.Users.AsNoTracking()
            .Where(u => u.TelegramChatId != null)
            .Select(u => new { u.Id, u.TelegramChatId })
            .ToListAsync(ct);
        return rows.Select(r => (r.Id, r.TelegramChatId!.Value)).ToList();
    }

    internal async Task TryNudgeAsync(string token, Guid userId, long chatId, CancellationToken ct)
    {
        using var scope = _scope.CreateScope();
        scope.ServiceProvider.GetRequiredService<CurrentUser>().ActAs(userId);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var today = DateTime.Now.Date;
        var key = $"{NudgeDateKey}:{userId}";
        if (await AlreadySentAsync(db, key, today)) return;

        var hasMeals = await db.MealEntries.AnyAsync(m => m.LoggedAt >= today && m.LoggedAt < today.AddDays(1), ct);
        await MarkSentAsync(db, key, today); // kayıt varsa da işaretle — bugün bir daha bakma
        if (hasMeals) return;

        await TelegramBotService.SendTelegram(_http, token, chatId,
            $"Saat {DateTime.Now:HH:mm}, bugün hiç öğün kaydı yok. Ne yediysen yaz, kaydedeyim.");
    }

    internal async Task TrySummaryAsync(string token, Guid userId, long chatId, CancellationToken ct)
    {
        using var scope = _scope.CreateScope();
        scope.ServiceProvider.GetRequiredService<CurrentUser>().ActAs(userId);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var today = DateTime.Now.Date;
        var key = $"{SummaryDateKey}:{userId}";
        if (await AlreadySentAsync(db, key, today)) return;

        var coach = scope.ServiceProvider.GetRequiredService<CoachService>();

        // Hiç veri yoksa ya da anahtar yoksa özet üretme (dürtme zaten gitti).
        var hasAny = await db.MealEntries.AnyAsync(m => m.LoggedAt >= today, ct)
                  || await db.WeightLogs.AnyAsync(w => w.LoggedAt >= today, ct)
                  || await db.WorkoutSessions.AnyAsync(w => w.LoggedAt >= today, ct);
        await MarkSentAsync(db, key, today);
        if (!hasAny || !await coach.HasKeyAsync(ct)) return;

        var messages = new JsonArray
        {
            new JsonObject
            {
                ["role"] = "user",
                ["content"] = "Günü kapat: bugünün kısa özetini çıkar. Kalori/makro vs hedef, antrenman varsa hacim, dikkat çeken 1-2 gözlem, yarın için tek somut öneri. Maksimum 6-7 satır. Gerekirse geçmiş tool'larına bak.",
            },
        };

        var result = await coach.ChatAsync(messages,
            extraSystem: "Bu otomatik gece özeti — kullanıcı mesaj atmadı, sen kendiliğinden yazıyorsun. Veri kaydetme tool'larını KULLANMA, sadece okuma.",
            readOnly: true,
            ct: ct);

        if (result.Ok && !string.IsNullOrWhiteSpace(result.Reply))
        {
            db.CoachMessages.Add(new CoachMessageRecord { Id = Guid.NewGuid(), Role = "assistant", Content = result.Reply, CreatedAt = DateTime.Now });
            await db.SaveChangesAsync(ct);
            await TelegramBotService.SendTelegram(_http, token, chatId, result.Reply);
        }
    }

    static async Task<bool> AlreadySentAsync(AppDbContext db, string key, DateTime day)
    {
        var setting = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == key);
        return setting?.Value == day.ToString("yyyy-MM-dd");
    }

    static async Task MarkSentAsync(AppDbContext db, string key, DateTime day)
    {
        var val = day.ToString("yyyy-MM-dd");
        var setting = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == key);
        if (setting is null) db.AppSettings.Add(new AppSetting { Key = key, Value = val });
        else setting.Value = val;
        await db.SaveChangesAsync();
    }
}
