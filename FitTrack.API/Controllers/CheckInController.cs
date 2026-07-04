using FitTrack.API.Data;
using FitTrack.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FitTrack.API.Controllers;

[ApiController]
[Route("api/checkin")]
public class CheckInController : ControllerBase
{
    private readonly AppDbContext _db;
    public CheckInController(AppDbContext db) => _db = db;

    // POST /api/checkin  → log a mood/energy/hunger snapshot
    [HttpPost]
    public async Task<ActionResult<CheckIn>> Log(LogCheckInRequest req)
    {
        var entry = new CheckIn
        {
            Id = Guid.NewGuid(),
            Mood = Math.Clamp(req.Mood, 1, 5),
            Energy = Math.Clamp(req.Energy, 1, 5),
            Hunger = Math.Clamp(req.Hunger, 1, 5),
            Note = req.Note,
            Context = req.Context ?? "general",
            LoggedAt = DateTime.UtcNow,
        };
        _db.CheckIns.Add(entry);
        await _db.SaveChangesAsync();
        return Ok(entry);
    }

    // GET /api/checkin/recent?take=20  → newest first
    [HttpGet("recent")]
    public async Task<ActionResult<List<CheckIn>>> Recent([FromQuery] int take = 20)
    {
        if (take <= 0) take = 20;
        return await _db.CheckIns
            .OrderByDescending(c => c.LoggedAt)
            .Take(take)
            .ToListAsync();
    }

    // GET /api/checkin/today
    [HttpGet("today")]
    public async Task<ActionResult<List<CheckIn>>> Today()
    {
        var day = DateTime.UtcNow.Date;
        return await _db.CheckIns
            .Where(c => c.LoggedAt >= day && c.LoggedAt < day.AddDays(1))
            .OrderByDescending(c => c.LoggedAt)
            .ToListAsync();
    }

    // DELETE /api/checkin/{id}
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var entry = await _db.CheckIns.FindAsync(id);
        if (entry is null) return NotFound();
        _db.CheckIns.Remove(entry);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
