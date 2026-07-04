using FitTrack.API.Data;
using FitTrack.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FitTrack.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WeightLogsController : ControllerBase
{
    private readonly AppDbContext _db;
    public WeightLogsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<WeightLog>>> GetAll()
        => await _db.WeightLogs.OrderByDescending(w => w.LoggedAt).ToListAsync();

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WeightLog>> Get(Guid id)
    {
        var log = await _db.WeightLogs.FindAsync(id);
        return log is null ? NotFound() : log;
    }

    [HttpPost]
    public async Task<ActionResult<WeightLog>> Create(WeightLog log)
    {
        log.Id = Guid.NewGuid();
        if (log.LoggedAt == default) log.LoggedAt = DateTime.UtcNow;
        _db.WeightLogs.Add(log);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = log.Id }, log);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, WeightLog input)
    {
        var log = await _db.WeightLogs.FindAsync(id);
        if (log is null) return NotFound();

        log.WeightKg = input.WeightKg;
        log.LoggedAt = input.LoggedAt;
        log.Notes = input.Notes;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var log = await _db.WeightLogs.FindAsync(id);
        if (log is null) return NotFound();
        _db.WeightLogs.Remove(log);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
