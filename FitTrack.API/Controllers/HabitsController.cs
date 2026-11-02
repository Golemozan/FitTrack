using FitTrack.API.Data;
using FitTrack.API.Security;
using FitTrack.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FitTrack.API.Controllers;

/// <summary>
/// Günlük alışkanlıklar (to-do'nun fitness hali): su, kreatin, adım… Her gün sıfırdan başlar.
/// İlk açılışta "Su — 8 bardak" hazır gelir; kullanıcı istediğini ekler, arşivler.
/// </summary>
[ApiController]
[Route("api/habits")]
public class HabitsController : ControllerBase
{
    public static readonly HashSet<string> Icons = new() { "water", "pill", "steps", "stretch", "sleep", "book", "apple", "check" };
    private const int MaxActive = 12;
    private static readonly TimeSpan MaxBackdate = TimeSpan.FromDays(7);

    private readonly AppDbContext _db;
    public HabitsController(AppDbContext db) => _db = db;

    // GET /api/habits?date=yyyy-MM-dd → o günün sayılarıyla aktif alışkanlıklar
    [HttpGet]
    public async Task<ActionResult<List<HabitToday>>> List([FromQuery] DateTime? date, CancellationToken ct)
    {
        var day = (date ?? DateTime.Now).Date;
        if (!DataRules.InWindow(day)) return BadRequest(new { error = "Tarih geçersiz." });
        await EnsureDefaultsAsync(ct);
        var habits = await _db.Habits.AsNoTracking().Where(h => !h.IsArchived).OrderBy(h => h.SortOrder).ToListAsync(ct);
        var ids = habits.Select(h => h.Id).ToList();
        var counts = await _db.HabitLogs.AsNoTracking()
            .Where(l => ids.Contains(l.HabitId) && l.Day == day)
            .ToDictionaryAsync(l => l.HabitId, l => l.Count, ct);

        return habits.Select(h => new HabitToday
        {
            Id = h.Id, Name = h.Name, Icon = h.Icon, Target = h.Target, Unit = h.Unit, SortOrder = h.SortOrder,
            Count = counts.GetValueOrDefault(h.Id),
        }).ToList();
    }

    [HttpPost]
    public async Task<ActionResult<Habit>> Create(HabitRequest req, CancellationToken ct)
    {
        var error = Validate(req);
        if (error is not null) return BadRequest(new { error });
        if (await _db.Habits.CountAsync(h => !h.IsArchived, ct) >= MaxActive)
            return BadRequest(new { error = $"En fazla {MaxActive} alışkanlık takip edilebilir." });

        var order = await _db.Habits.Select(h => (int?)h.SortOrder).MaxAsync(ct) ?? -1;
        var habit = new Habit
        {
            Id = Guid.NewGuid(), Name = req.Name.Trim(), Icon = req.Icon, Target = req.Target,
            Unit = string.IsNullOrWhiteSpace(req.Unit) ? null : req.Unit.Trim(),
            SortOrder = order + 1, CreatedAt = DateTime.Now,
        };
        _db.Habits.Add(habit);
        await _db.SaveChangesAsync(ct);
        return Ok(habit);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<Habit>> Update(Guid id, HabitRequest req, CancellationToken ct)
    {
        var error = Validate(req);
        if (error is not null) return BadRequest(new { error });
        var habit = await _db.Habits.FirstOrDefaultAsync(h => h.Id == id && !h.IsArchived, ct);
        if (habit is null) return NotFound();
        habit.Name = req.Name.Trim();
        habit.Icon = req.Icon;
        habit.Target = req.Target;
        habit.Unit = string.IsNullOrWhiteSpace(req.Unit) ? null : req.Unit.Trim();
        await _db.SaveChangesAsync(ct);
        return Ok(habit);
    }

    /// <summary>Silmek yerine arşivler: geçmiş günlerin kayıtları haftalık özette kalır.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    {
        var habit = await _db.Habits.FirstOrDefaultAsync(h => h.Id == id && !h.IsArchived, ct);
        if (habit is null) return NotFound();
        habit.IsArchived = true;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // PUT /api/habits/{id}/log  { date?, count } → o günün sayısını ayarla (istemci +1/−1'i kendisi hesaplar)
    [HttpPut("{id:guid}/log")]
    public async Task<ActionResult<HabitToday>> Log(Guid id, HabitLogRequest req, CancellationToken ct)
    {
        var habit = await _db.Habits.FirstOrDefaultAsync(h => h.Id == id && !h.IsArchived, ct);
        if (habit is null) return NotFound();

        var day = (req.Date ?? DateTime.Now).Date;
        if (day > DateTime.Now.Date) return BadRequest(new { error = "Gelecek bir güne kayıt girilemez." });
        if (day < DateTime.Now.Date - MaxBackdate) return BadRequest(new { error = "En fazla 7 gün geriye kayıt girilebilir." });
        var count = Math.Clamp(req.Count, 0, Math.Max(habit.Target * 3, 20));

        var log = await _db.HabitLogs.FirstOrDefaultAsync(l => l.HabitId == id && l.Day == day, ct);
        if (log is null)
        {
            log = new HabitLog { Id = Guid.NewGuid(), HabitId = id, Day = day, Count = count };
            _db.HabitLogs.Add(log);
        }
        else log.Count = count;
        await _db.SaveChangesAsync(ct);

        return Ok(new HabitToday
        {
            Id = habit.Id, Name = habit.Name, Icon = habit.Icon, Target = habit.Target, Unit = habit.Unit,
            SortOrder = habit.SortOrder, Count = log.Count,
        });
    }

    /// <summary>Hiç alışkanlığı olmayan (arşivlenmiş dahil) kullanıcıya "Su" hazır gelir. Bir kez.</summary>
    private async Task EnsureDefaultsAsync(CancellationToken ct)
    {
        if (await _db.Habits.AnyAsync(ct)) return;
        using var _ = await UserLocks.AcquireAsync(CurrentUser.FromPrincipal(User), "habits", ct);
        if (await _db.Habits.AnyAsync(ct)) return; // kilidi bekleyen ikinci istek: ilki zaten oluşturdu
        _db.Habits.Add(new Habit
        {
            Id = Guid.NewGuid(), Name = "Su", Icon = "water", Target = 8, Unit = "bardak", SortOrder = 0, CreatedAt = DateTime.Now,
        });
        await _db.SaveChangesAsync(ct);
    }

    private static string? Validate(HabitRequest req)
    {
        var name = (req.Name ?? "").Trim();
        if (name.Length is < 1 or > 40) return "İsim 1-40 karakter olmalı.";
        if (!Icons.Contains(req.Icon)) return "Geçersiz ikon.";
        if (req.Target is < 1 or > 50) return "Hedef 1-50 arasında olmalı.";
        if (req.Unit is { Length: > 15 }) return "Birim en fazla 15 karakter olabilir.";
        return null;
    }
}
