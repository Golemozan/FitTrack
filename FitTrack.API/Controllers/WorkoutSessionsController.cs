using FitTrack.API.Data;
using FitTrack.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FitTrack.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WorkoutSessionsController : ControllerBase
{
    private readonly AppDbContext _db;
    public WorkoutSessionsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<WorkoutSession>>> GetAll()
        => await _db.WorkoutSessions
            .Include(w => w.Exercises).ThenInclude(e => e.Sets)
            .OrderByDescending(w => w.LoggedAt)
            .ToListAsync();

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WorkoutSession>> Get(Guid id)
    {
        var session = await _db.WorkoutSessions
            .Include(w => w.Exercises).ThenInclude(e => e.Sets)
            .FirstOrDefaultAsync(w => w.Id == id);
        return session is null ? NotFound() : session;
    }

    [HttpPost]
    public async Task<ActionResult<WorkoutSession>> Create(WorkoutSession session)
    {
        NormalizeGraph(session);
        if (session.LoggedAt == default) session.LoggedAt = DateTime.UtcNow;
        _db.WorkoutSessions.Add(session);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = session.Id }, session);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, WorkoutSession input)
    {
        var session = await _db.WorkoutSessions
            .Include(w => w.Exercises).ThenInclude(e => e.Sets)
            .FirstOrDefaultAsync(w => w.Id == id);
        if (session is null) return NotFound();

        session.Name = input.Name;
        session.LoggedAt = input.LoggedAt;

        // Replace the exercise/set graph wholesale (cascade removes orphans).
        _db.Exercises.RemoveRange(session.Exercises);
        session.Exercises = input.Exercises;
        NormalizeGraph(session);

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var session = await _db.WorkoutSessions.FindAsync(id);
        if (session is null) return NotFound();
        _db.WorkoutSessions.Remove(session);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // Assign fresh ids and wire foreign keys for the whole graph.
    private static void NormalizeGraph(WorkoutSession session)
    {
        if (session.Id == Guid.Empty) session.Id = Guid.NewGuid();
        foreach (var exercise in session.Exercises)
        {
            exercise.Id = Guid.NewGuid();
            exercise.WorkoutSessionId = session.Id;
            foreach (var set in exercise.Sets)
            {
                set.Id = Guid.NewGuid();
                set.ExerciseId = exercise.Id;
            }
        }
    }
}
