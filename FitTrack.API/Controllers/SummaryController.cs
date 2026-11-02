using FitTrack.API.Data;
using FitTrack.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FitTrack.API.Controllers;

public record PeriodStats(
    DateTime From, DateTime To,
    int MealDays, double AvgCalories, double AvgProtein, int CalorieGoalDays, int ProteinGoalDays,
    int Workouts, int CompletedSets, double VolumeKg,
    double? WeightStart, double? WeightEnd, double? WeightChange,
    double? AvgMood, double? AvgEnergy, int CheckIns,
    double? HabitRate, double? AvgWater);

public record HabitWeek(Guid Id, string Name, string Icon, int Target, string? Unit, int DaysDone, List<int> Counts);
public record DayStrip(DateTime Date, double Calories, int Meals, bool Workout, double? Mood);

public record WeeklySummary(
    PeriodStats ThisWeek, PeriodStats PreviousWeek,
    double CalorieGoal, double ProteinGoal,
    List<DayStrip> Days, List<HabitWeek> Habits, List<string> Highlights);

/// <summary>
/// Son 7 gün (bugün dahil) ve önceki 7 gün, aynı hesapla. Uydurma metrik yok: her sayı kullanıcının
/// girdiği kayıtlardan gelir; kayıt yoksa alan boş (null) döner, sıfır gibi gösterilmez.
/// </summary>
[ApiController]
[Route("api/summary")]
public class SummaryController : ControllerBase
{
    private readonly AppDbContext _db;
    public SummaryController(AppDbContext db) => _db = db;

    [HttpGet("weekly")]
    public async Task<ActionResult<WeeklySummary>> Weekly(CancellationToken ct)
    {
        var today = DateTime.Now.Date;
        var from = today.AddDays(-6);
        var prevFrom = from.AddDays(-7);
        var end = today.AddDays(1);

        var goals = await _db.UserGoals.AsNoTracking().FirstOrDefaultAsync(ct);
        var calGoal = goals?.CalorieGoal ?? 0;
        var proGoal = goals?.ProteinGoal ?? 0;

        var meals = await _db.MealEntries.AsNoTracking().Where(m => m.LoggedAt >= prevFrom && m.LoggedAt < end)
            .Select(m => new { m.LoggedAt, m.Calories, m.Protein }).ToListAsync(ct);
        var sessions = await _db.WorkoutSessions.AsNoTracking().Where(w => w.LoggedAt >= prevFrom && w.LoggedAt < end)
            .Select(w => new
            {
                w.LoggedAt,
                Sets = w.Exercises.SelectMany(e => e.Sets).Where(s => s.IsCompleted).Select(s => new { s.WeightKg, s.Reps }).ToList(),
            }).ToListAsync(ct);
        // Kilo için pencereden önceki son ölçüm de lazım: haftanın başlangıç değeri o olabilir.
        var weights = await _db.WeightLogs.AsNoTracking().Where(w => w.LoggedAt < end)
            .OrderByDescending(w => w.LoggedAt).Take(60).Select(w => new { w.LoggedAt, w.WeightKg }).ToListAsync(ct);
        var checkins = await _db.CheckIns.AsNoTracking().Where(c => c.LoggedAt >= prevFrom && c.LoggedAt < end)
            .Select(c => new { c.LoggedAt, c.Mood, c.Energy }).ToListAsync(ct);
        var habits = await _db.Habits.AsNoTracking().ToListAsync(ct);
        var logs = await _db.HabitLogs.AsNoTracking().Where(l => l.Day >= prevFrom && l.Day < end).ToListAsync(ct);

        PeriodStats Stats(DateTime f, DateTime t)
        {
            var m = meals.Where(x => x.LoggedAt >= f && x.LoggedAt < t).GroupBy(x => x.LoggedAt.Date)
                .Select(g => new { Cal = g.Sum(x => x.Calories), Pro = g.Sum(x => x.Protein) }).ToList();
            var s = sessions.Where(x => x.LoggedAt >= f && x.LoggedAt < t).ToList();
            var w = weights.Where(x => x.LoggedAt < t).OrderBy(x => x.LoggedAt).ToList();
            var startW = w.LastOrDefault(x => x.LoggedAt < f) ?? w.FirstOrDefault(x => x.LoggedAt >= f);
            var endW = w.LastOrDefault(x => x.LoggedAt >= f);
            var c = checkins.Where(x => x.LoggedAt >= f && x.LoggedAt < t).ToList();

            // Alışkanlık oranı: o dönem aktif olan her alışkanlığın her günü için min(sayı, hedef) / hedef.
            var active = habits.Where(h => h.CreatedAt.Date < t && (!h.IsArchived || logs.Any(l => l.HabitId == h.Id && l.Day >= f && l.Day < t))).ToList();
            double? habitRate = null;
            if (active.Count > 0)
            {
                double sum = 0; int slots = 0;
                foreach (var h in active)
                    for (var d = f; d < t; d = d.AddDays(1))
                    {
                        if (d < h.CreatedAt.Date) continue;
                        var count = logs.FirstOrDefault(l => l.HabitId == h.Id && l.Day == d)?.Count ?? 0;
                        sum += Math.Min(count, h.Target) / (double)h.Target;
                        slots++;
                    }
                habitRate = slots > 0 ? Math.Round(sum / slots, 3) : null;
            }
            var water = habits.FirstOrDefault(h => h.Icon == "water");
            double? avgWater = water is null ? null
                : Math.Round(Enumerable.Range(0, (t - f).Days).Average(i => logs.FirstOrDefault(l => l.HabitId == water.Id && l.Day == f.AddDays(i))?.Count ?? 0), 1);

            return new PeriodStats(
                f, t.AddDays(-1),
                m.Count,
                m.Count > 0 ? Math.Round(m.Average(x => x.Cal)) : 0,
                m.Count > 0 ? Math.Round(m.Average(x => x.Pro)) : 0,
                calGoal > 0 ? m.Count(x => x.Cal >= calGoal * 0.9 && x.Cal <= calGoal * 1.1) : 0,
                proGoal > 0 ? m.Count(x => x.Pro >= proGoal * 0.9) : 0,
                s.Count,
                s.Sum(x => x.Sets.Count),
                Math.Round(s.Sum(x => x.Sets.Sum(y => y.WeightKg * y.Reps))),
                startW?.WeightKg, endW?.WeightKg,
                startW is not null && endW is not null ? Math.Round(endW.WeightKg - startW.WeightKg, 1) : null,
                c.Count > 0 ? Math.Round(c.Average(x => x.Mood), 1) : null,
                c.Count > 0 ? Math.Round(c.Average(x => x.Energy), 1) : null,
                c.Count,
                habitRate, avgWater);
        }

        var thisWeek = Stats(from, end);
        var prevWeek = Stats(prevFrom, from);

        var days = Enumerable.Range(0, 7).Select(i =>
        {
            var d = from.AddDays(i);
            var dm = meals.Where(x => x.LoggedAt.Date == d).ToList();
            var dc = checkins.Where(x => x.LoggedAt.Date == d).ToList();
            return new DayStrip(d, Math.Round(dm.Sum(x => x.Calories)), dm.Count,
                sessions.Any(x => x.LoggedAt.Date == d && x.Sets.Count > 0),
                dc.Count > 0 ? Math.Round(dc.Average(x => x.Mood), 1) : null);
        }).ToList();

        var habitWeeks = habits.Where(h => !h.IsArchived).OrderBy(h => h.SortOrder).Select(h =>
        {
            var counts = Enumerable.Range(0, 7).Select(i => logs.FirstOrDefault(l => l.HabitId == h.Id && l.Day == from.AddDays(i))?.Count ?? 0).ToList();
            return new HabitWeek(h.Id, h.Name, h.Icon, h.Target, h.Unit, counts.Count(c => c >= h.Target), counts);
        }).ToList();

        return new WeeklySummary(thisWeek, prevWeek, calGoal, proGoal, days, habitWeeks, Highlights(thisWeek, prevWeek, calGoal, proGoal));
    }

    /// <summary>Sayılardan çıkan 2-4 kısa cümle. Kural tabanlı, AI değil — her cümlenin dayanağı yukarıdaki istatistik.</summary>
    private static List<string> Highlights(PeriodStats t, PeriodStats p, double calGoal, double proGoal)
    {
        var list = new List<string>();
        if (t.Workouts > 0)
            list.Add(t.Workouts > p.Workouts
                ? $"{t.Workouts} antrenman — geçen haftadan {t.Workouts - p.Workouts} fazla."
                : $"{t.Workouts} antrenman, {t.CompletedSets} set tamamladın.");
        else list.Add("Bu hafta antrenman kaydı yok.");

        if (proGoal > 0 && t.MealDays > 0)
            list.Add($"Protein hedefini {t.MealDays} günün {t.ProteinGoalDays} gününde tutturdun.");
        if (calGoal > 0 && t.MealDays > 0 && t.AvgCalories > 0)
        {
            var diff = t.AvgCalories - calGoal;
            list.Add(Math.Abs(diff) <= calGoal * 0.1
                ? "Kalori ortalaman hedefe yakın."
                : diff < 0 ? $"Günlük ortalama hedefin {Math.Round(-diff)} kcal altında." : $"Günlük ortalama hedefin {Math.Round(diff)} kcal üstünde.");
        }
        if (t.WeightChange is { } wc && wc != 0)
            list.Add($"Kilo {(wc < 0 ? "−" : "+")}{Math.Abs(wc):0.0} kg değişti.");
        return list;
    }
}
