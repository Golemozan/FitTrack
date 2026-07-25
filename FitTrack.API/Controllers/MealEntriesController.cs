using FitTrack.API.Data;
using FitTrack.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FitTrack.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MealEntriesController : ControllerBase
{
    private readonly AppDbContext _db;
    public MealEntriesController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<MealEntry>>> GetAll([FromQuery] DateTime? date)
    {
        var query = _db.MealEntries.AsQueryable();
        if (date is not null)
        {
            var day = date.Value.Date;
            query = query.Where(m => m.LoggedAt >= day && m.LoggedAt < day.AddDays(1));
        }
        return await query.OrderByDescending(m => m.LoggedAt).ToListAsync();
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MealEntry>> Get(Guid id)
    {
        var entry = await _db.MealEntries.FindAsync(id);
        return entry is null ? NotFound() : entry;
    }

    [HttpPost]
    public async Task<ActionResult<MealEntry>> Create(MealEntry entry)
    {
        entry.Id = Guid.NewGuid();
        if (entry.LoggedAt == default) entry.LoggedAt = DateTime.Now;
        _db.MealEntries.Add(entry);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = entry.Id }, entry);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, MealEntry input)
    {
        var entry = await _db.MealEntries.FindAsync(id);
        if (entry is null) return NotFound();

        entry.FoodName = input.FoodName;
        entry.Grams = input.Grams;
        entry.Calories = input.Calories;
        entry.Protein = input.Protein;
        entry.Carbs = input.Carbs;
        entry.Fat = input.Fat;
        entry.MealType = input.MealType;
        entry.LoggedAt = input.LoggedAt;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var entry = await _db.MealEntries.FindAsync(id);
        if (entry is null) return NotFound();
        _db.MealEntries.Remove(entry);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
