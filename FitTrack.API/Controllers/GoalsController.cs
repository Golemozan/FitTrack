using FitTrack.API.Data;
using FitTrack.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FitTrack.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GoalsController : ControllerBase
{
    private readonly AppDbContext _db;
    public GoalsController(AppDbContext db) => _db = db;

    // Single-row goals: fetch existing or lazily create the default row.
    private async Task<UserGoals> GetOrCreateAsync()
    {
        // Filtreli: yalnız oturumdaki kullanıcının hedefi. Yoksa ona özel varsayılan açılır.
        var goals = await _db.UserGoals.OrderBy(g => g.UpdatedAt).FirstOrDefaultAsync();
        if (goals is null)
        {
            goals = new UserGoals
            {
                Id = Guid.NewGuid(),
                CalorieGoal = 2500,
                ProteinGoal = 180,
                CarbGoal = 250,
                FatGoal = 70,
                UpdatedAt = DateTime.UtcNow,
            };
            _db.UserGoals.Add(goals);
            await _db.SaveChangesAsync();
        }
        return goals;
    }

    // GET /api/goals
    [HttpGet]
    public async Task<ActionResult<UserGoals>> Get() => await GetOrCreateAsync();

    // PUT /api/goals
    [HttpPut]
    public async Task<ActionResult<UserGoals>> Update(UpdateGoalsRequest req)
    {
        var goals = await GetOrCreateAsync();
        goals.CalorieGoal = req.CalorieGoal;
        goals.ProteinGoal = req.ProteinGoal;
        goals.CarbGoal = req.CarbGoal;
        goals.FatGoal = req.FatGoal;
        goals.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return goals;
    }
}
