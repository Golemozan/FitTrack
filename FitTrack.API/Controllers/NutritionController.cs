using FitTrack.API.Data;
using FitTrack.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FitTrack.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NutritionController : ControllerBase
{
    private readonly AppDbContext _db;

    public NutritionController(AppDbContext db)
    {
        _db = db;
    }

    // Food search is now fully client-side (local food DB, no external API).

    // POST /api/nutrition/log
    [HttpPost("log")]
    public async Task<ActionResult<MealEntry>> Log(LogFoodRequest req)
    {
        var entry = new MealEntry
        {
            Id = Guid.NewGuid(),
            FoodName = req.FoodName,
            Grams = req.Grams,
            Calories = req.Calories,
            Protein = req.Protein,
            Carbs = req.Carbs,
            Fat = req.Fat,
            MealType = req.MealType,
            LoggedAt = req.LogDate ?? DateTime.Now,
        };
        _db.MealEntries.Add(entry);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Today), null, entry);
    }

    // PUT /api/nutrition/log/{id}  → edit an existing entry (food, grams, macros, meal)
    [HttpPut("log/{id:guid}")]
    public async Task<ActionResult<MealEntry>> UpdateLog(Guid id, LogFoodRequest req)
    {
        var entry = await _db.MealEntries.FirstOrDefaultAsync(m => m.Id == id);
        if (entry is null) return NotFound();

        entry.FoodName = req.FoodName;
        entry.Grams = req.Grams;
        entry.Calories = req.Calories;
        entry.Protein = req.Protein;
        entry.Carbs = req.Carbs;
        entry.Fat = req.Fat;
        entry.MealType = req.MealType;

        await _db.SaveChangesAsync();
        return entry;
    }

    // GET /api/nutrition/today  → today's entries grouped by meal type
    [HttpGet("today")]
    public async Task<ActionResult<object>> Today()
    {
        var today = DateTime.Now.Date;
        var entries = await _db.MealEntries
            .Where(m => m.LoggedAt >= today && m.LoggedAt < today.AddDays(1))
            .OrderBy(m => m.LoggedAt)
            .ToListAsync();

        var grouped = entries
            .GroupBy(m => m.MealType)
            .ToDictionary(g => g.Key.ToString(), g => g.ToList());

        return Ok(grouped);
    }

    // GET /api/nutrition/summary?date=2025-01-15
    [HttpGet("summary")]
    public async Task<ActionResult<NutritionSummary>> Summary([FromQuery] DateTime? date)
    {
        var day = (date ?? DateTime.Now).Date;
        var entries = await _db.MealEntries
            .Where(m => m.LoggedAt >= day && m.LoggedAt < day.AddDays(1))
            .ToListAsync();

        return new NutritionSummary
        {
            TotalCalories = entries.Sum(m => m.Calories),
            TotalProtein = entries.Sum(m => m.Protein),
            TotalCarbs = entries.Sum(m => m.Carbs),
            TotalFat = entries.Sum(m => m.Fat),
        };
    }

    // DELETE /api/nutrition/log/{id}
    [HttpDelete("log/{id:guid}")]
    public async Task<IActionResult> DeleteLog(Guid id)
    {
        var entry = await _db.MealEntries.FirstOrDefaultAsync(m => m.Id == id);
        if (entry is null) return NotFound();
        _db.MealEntries.Remove(entry);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // GET /api/nutrition/day/{date} → meals for a specific day (YYYY-MM-DD)
    [HttpGet("day/{date}")]
    public async Task<ActionResult<object>> Day(string date)
    {
        if (!DateTime.TryParse(date, out var day))
            return BadRequest(new { error = "Geçersiz tarih formatı. YYYY-MM-DD kullan." });

        var entries = await _db.MealEntries
            .Where(m => m.LoggedAt >= day.Date && m.LoggedAt < day.Date.AddDays(1))
            .OrderBy(m => m.LoggedAt)
            .ToListAsync();

        var grouped = entries
            .GroupBy(m => m.MealType)
            .ToDictionary(g => g.Key.ToString(), g => g.ToList());

        return Ok(grouped);
    }

    // GET /api/nutrition/history?days=30 → daily macro summaries
    [HttpGet("history")]
    public async Task<ActionResult<List<DailyNutritionSummary>>> History([FromQuery] int days = 30)
    {
        var since = DateTime.Now.Date.AddDays(-days + 1);
        var entries = await _db.MealEntries
            .Where(m => m.LoggedAt >= since)
            .ToListAsync();

        var daily = entries
            .GroupBy(m => m.LoggedAt.Date)
            .OrderBy(g => g.Key)
            .Select(g => new DailyNutritionSummary
            {
                Date = g.Key,
                TotalCalories = g.Sum(m => m.Calories),
                TotalProtein = g.Sum(m => m.Protein),
                TotalCarbs = g.Sum(m => m.Carbs),
                TotalFat = g.Sum(m => m.Fat),
                MealCount = g.Count(),
            })
            .ToList();

        return Ok(daily);
    }

    // GET /api/nutrition/streak → consecutive days with at least 1 meal logged
    [HttpGet("streak")]
    public async Task<ActionResult<NutritionStreak>> Streak()
    {
        var today = DateTime.Now.Date;
        var streak = 0;

        for (var i = 0; i < 365; i++)
        {
            var day = today.AddDays(-i);
            var hasMeal = await _db.MealEntries
                .AnyAsync(m => m.LoggedAt >= day && m.LoggedAt < day.AddDays(1));
            if (hasMeal) streak++;
            else break;
        }

        return Ok(new NutritionStreak { Days = streak });
    }
}
