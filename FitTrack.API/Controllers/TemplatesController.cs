using FitTrack.API.Data;
using FitTrack.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FitTrack.API.Controllers;

/// <summary>
/// Antrenman şablonları. Bir seans şablon olarak kaydedilir; şablon bugüne uygulanınca hareketler
/// ve setler hazır gelir, ağırlık/tekrar o hareketin son yapıldığı seanstan doldurulur.
/// </summary>
[ApiController]
[Route("api/workout/templates")]
public class TemplatesController : ControllerBase
{
    private const int MaxTemplates = 20;
    private readonly AppDbContext _db;
    public TemplatesController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<List<WorkoutTemplate>>> List(CancellationToken ct)
    {
        var list = await _db.WorkoutTemplates.AsNoTracking().Include(t => t.Exercises)
            .OrderByDescending(t => t.LastUsedAt ?? t.CreatedAt).ToListAsync(ct);
        foreach (var t in list) t.Exercises = t.Exercises.OrderBy(e => e.SortOrder).ToList();
        return list;
    }

    // POST /api/workout/templates { name, sessionId } → seansın hareketlerinden şablon
    [HttpPost]
    public async Task<ActionResult<WorkoutTemplate>> Save(SaveTemplateRequest req, CancellationToken ct)
    {
        var name = (req.Name ?? "").Trim();
        if (name.Length is < 1 or > 40) return BadRequest(new { error = "Şablon adı 1-40 karakter olmalı." });
        // Aynı isim varsa yeni kopya açılmaz, mevcut kayıt güncellenir: "Push günü"nü değiştirip tekrar kaydetmek.
        var all = await _db.WorkoutTemplates.Include(t => t.Exercises).ToListAsync(ct);
        var existing = all.FirstOrDefault(t => string.Equals(t.Name.Trim(), name, StringComparison.CurrentCultureIgnoreCase));
        if (existing is null && all.Count >= MaxTemplates)
            return BadRequest(new { error = $"En fazla {MaxTemplates} antrenman kaydedilebilir." });

        var session = await _db.WorkoutSessions.AsNoTracking()
            .Include(s => s.Exercises.OrderBy(e => e.CreatedAt).ThenBy(e => e.Id)).ThenInclude(e => e.Sets)
            .FirstOrDefaultAsync(s => s.Id == req.SessionId, ct);
        if (session is null) return NotFound();
        if (session.Exercises.Count == 0) return BadRequest(new { error = "Boş antrenman şablon olarak kaydedilemez." });

        WorkoutTemplate template;
        if (existing is not null)
        {
            template = existing;
            _db.TemplateExercises.RemoveRange(existing.Exercises);
            existing.Exercises.Clear();
            existing.Name = name;
        }
        else
        {
            template = new WorkoutTemplate { Id = Guid.NewGuid(), Name = name, CreatedAt = DateTime.Now };
            _db.WorkoutTemplates.Add(template);
        }
        var order = 0;
        // Exercise'ın sıra kolonu yok; şablon, seansın ekranda gösterildiği sırayı korur.
        foreach (var ex in session.Exercises)
        {
            var sets = ex.Sets.OrderBy(s => s.SetNumber).ToList();
            // Açıkça Added: Guid anahtarı dolu yeni çocuk, takip edilen ebeveyne eklenince EF onu
            // "var olan" sanıp UPDATE dener (aynı isimle güncellemede 0 satır → hata).
            var te = new TemplateExercise
            {
                Id = Guid.NewGuid(),
                WorkoutTemplateId = template.Id,
                Name = ex.Name,
                MuscleGroup = ex.MuscleGroup,
                SortOrder = order++,
                SetCount = Math.Clamp(sets.Count, 1, 10),
                TargetReps = sets.Count > 0 ? (int)Math.Round(sets.Average(s => s.Reps)) : 10,
            };
            template.Exercises.Add(te);
            _db.TemplateExercises.Add(te);
        }
        template.LastUsedAt = DateTime.Now;
        await _db.SaveChangesAsync(ct);
        template.Exercises = template.Exercises.OrderBy(e => e.SortOrder).ToList();
        return Ok(template);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var template = await _db.WorkoutTemplates.Include(t => t.Exercises).FirstOrDefaultAsync(t => t.Id == id, ct);
        if (template is null) return NotFound();
        _db.WorkoutTemplates.Remove(template);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// Şablonu bugünün seansına uygular (yoksa açar). Seans boşsa adı şablonun adı olur.
    /// Bugünde zaten olan hareket tekrar eklenmez. Setler tamamlanmamış gelir.
    /// </summary>
    [HttpPost("{id:guid}/apply")]
    public async Task<ActionResult<WorkoutSession>> Apply(Guid id, CancellationToken ct)
    {
        var template = await _db.WorkoutTemplates.Include(t => t.Exercises).FirstOrDefaultAsync(t => t.Id == id, ct);
        if (template is null) return NotFound();

        var day = DateTime.Now.Date;
        var session = await _db.WorkoutSessions
            .Include(w => w.Exercises.OrderBy(e => e.CreatedAt).ThenBy(e => e.Id)).ThenInclude(e => e.Sets)
            .Where(w => w.LoggedAt >= day && w.LoggedAt < day.AddDays(1))
            .OrderByDescending(w => w.LoggedAt)
            .FirstOrDefaultAsync(ct);
        if (session is null)
        {
            session = new WorkoutSession { Id = Guid.NewGuid(), Name = template.Name, LoggedAt = DateTime.Now };
            _db.WorkoutSessions.Add(session);
        }
        else if (session.Exercises.Count == 0)
        {
            session.Name = template.Name;
        }

        var existing = session.Exercises.Select(e => e.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var stamp = DateTime.Now;
        foreach (var te in template.Exercises.OrderBy(e => e.SortOrder))
        {
            if (existing.Contains(te.Name)) continue;

            // Son yapılan seansın setleri: ağırlık ve tekrar oradan. Hiç yapılmadıysa 0 kg × hedef tekrar.
            var last = await _db.Exercises.AsNoTracking()
                .Where(e => e.Name == te.Name && e.WorkoutSessionId != session.Id)
                .OrderByDescending(e => e.WorkoutSession!.LoggedAt)
                .Select(e => e.Sets.OrderBy(s => s.SetNumber).Select(s => new { s.WeightKg, s.Reps }).ToList())
                .FirstOrDefaultAsync(ct);

            var exercise = new Exercise
            {
                Id = Guid.NewGuid(), WorkoutSessionId = session.Id, Name = te.Name, MuscleGroup = te.MuscleGroup,
                CreatedAt = stamp = stamp.AddMilliseconds(1), // şablon sırası korunsun
            };
            for (var n = 1; n <= te.SetCount; n++)
            {
                var prior = last is { Count: > 0 } ? last[Math.Min(n, last.Count) - 1] : null;
                exercise.Sets.Add(new ExerciseSet
                {
                    Id = Guid.NewGuid(), ExerciseId = exercise.Id, SetNumber = n,
                    WeightKg = prior?.WeightKg ?? 0, Reps = prior?.Reps ?? te.TargetReps, IsCompleted = false,
                });
            }
            session.Exercises.Add(exercise);
            _db.Exercises.Add(exercise);
        }

        template.LastUsedAt = DateTime.Now;
        await _db.SaveChangesAsync(ct);
        session.Exercises = session.Exercises.OrderBy(e => e.CreatedAt).ThenBy(e => e.Id).ToList();
        return Ok(session);
    }
}
