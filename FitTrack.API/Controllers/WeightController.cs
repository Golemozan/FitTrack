using FitTrack.API.Data;
using FitTrack.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FitTrack.API.Controllers;

[ApiController]
[Route("api/weight")]
public class WeightController : ControllerBase
{
    private readonly AppDbContext _db;
    public WeightController(AppDbContext db) => _db = db;

    // Gün sınırları kullanıcının saatine göre hesaplanır. Konteynerin TZ'i
    // Europe/Istanbul olarak ayarlı (bkz. Dockerfile), bu yüzden DateTime.Now
    // doğru "bugün"ü verir. UtcNow kullanmak gece 00:00-03:00 arası girilen
    // kaydı bir önceki güne yazardı.
    [HttpPost("log")]
    public async Task<ActionResult<WeightLog>> Log(LogWeightRequest req)
    {
        var targetDay = (req.LoggedAt ?? DateTime.Now).Date;
        var existing = await _db.WeightLogs
            .FirstOrDefaultAsync(w => w.LoggedAt >= targetDay && w.LoggedAt < targetDay.AddDays(1));

        if (existing is not null)
        {
            existing.WeightKg = req.WeightKg;
            existing.Notes = req.Notes;
            existing.LoggedAt = targetDay; // keep the target day, don't overwrite to now
            await _db.SaveChangesAsync();
            return Ok(existing);
        }

        var log = new WeightLog
        {
            Id = Guid.NewGuid(),
            WeightKg = req.WeightKg,
            Notes = req.Notes,
            LoggedAt = targetDay,
        };
        _db.WeightLogs.Add(log);
        await _db.SaveChangesAsync();
        return Ok(log);
    }

    // 2. GET /api/weight/today
    [HttpGet("today")]
    public async Task<ActionResult<WeightLog?>> Today()
    {
        var day = DateTime.Now.Date;
        var log = await _db.WeightLogs
            .Where(w => w.LoggedAt >= day && w.LoggedAt < day.AddDays(1))
            .OrderByDescending(w => w.LoggedAt)
            .FirstOrDefaultAsync();
        // Kayit yoksa 204 degil, acikca 200 + `null` (bkz. WorkoutController.GetToday).
        return new JsonResult(log);
    }

    // 3. GET /api/weight/history?days=30  → ascending points
    [HttpGet("history")]
    public async Task<ActionResult<List<WeightPoint>>> History([FromQuery] int days = 30)
    {
        if (days <= 0) days = 30;
        var since = DateTime.Now.Date.AddDays(-days + 1);
        return await _db.WeightLogs
            .Where(w => w.LoggedAt >= since)
            .OrderBy(w => w.LoggedAt)
            .Select(w => new WeightPoint { LoggedAt = w.LoggedAt, WeightKg = w.WeightKg })
            .ToListAsync();
    }

    // 4. GET /api/weight/stats
    [HttpGet("stats")]
    public async Task<ActionResult<WeightStats>> Stats()
    {
        var logs = await _db.WeightLogs.OrderBy(w => w.LoggedAt).ToListAsync();
        if (logs.Count == 0) return new WeightStats();

        var current = logs[^1];
        var start = logs[0];

        // Weekly change: compare to the most recent log at or before 7 days ago,
        // falling back to the earliest log when there's no older sample.
        var weekAgo = DateTime.Now.AddDays(-7);
        var baseline = logs.LastOrDefault(w => w.LoggedAt <= weekAgo) ?? start;

        return new WeightStats
        {
            CurrentWeight = current.WeightKg,
            StartWeight = start.WeightKg,
            LowestWeight = logs.Min(w => w.WeightKg),
            HighestWeight = logs.Max(w => w.WeightKg),
            TotalChange = current.WeightKg - start.WeightKg,
            WeeklyChange = current.WeightKg - baseline.WeightKg,
            LastLoggedAt = current.LoggedAt,
        };
    }

    // GET /api/weight/logs?take=10  → recent full rows (with ids, for delete list)
    [HttpGet("logs")]
    public async Task<ActionResult<List<WeightLog>>> Logs([FromQuery] int take = 10)
    {
        if (take <= 0) take = 10;
        return await _db.WeightLogs
            .OrderByDescending(w => w.LoggedAt)
            .Take(take)
            .ToListAsync();
    }

    // 4b. PUT /api/weight/log/{id}  → edit any past log (weight + note)
    [HttpPut("log/{id:guid}")]
    public async Task<ActionResult<WeightLog>> Update(Guid id, LogWeightRequest req)
    {
        var log = await _db.WeightLogs.FirstOrDefaultAsync(w => w.Id == id);
        if (log is null) return NotFound();

        log.WeightKg = req.WeightKg;
        log.Notes = req.Notes;
        await _db.SaveChangesAsync();
        return Ok(log);
    }

    // 5. DELETE /api/weight/log/{id}
    [HttpDelete("log/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var log = await _db.WeightLogs.FirstOrDefaultAsync(w => w.Id == id);
        if (log is null) return NotFound();
        _db.WeightLogs.Remove(log);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
