using FitTrack.API.Data;
using FitTrack.API.Models;
using Microsoft.EntityFrameworkCore;

namespace FitTrack.API.Services;

/// <summary>
/// Başarımlar kullanıcının zaten girdiği veriden hesaplanır — ayrı bir olay kaydı yok, uydurma sayı yok.
/// Her tanımın 1-3 kademesi var (bronz/gümüş/altın). Eşik ilk geçildiğinde kademe <see cref="UserAchievement"/>
/// olarak yazılır: açılma tarihi kalıcıdır, "yeni" kutlaması bir kez çıkar, veri silinse de geri alınmaz.
///
/// İlk değerlendirme sessizdir: özellik gelmeden önce zaten hak edilmiş kademeler "görülmüş" yazılır.
/// Yoksa mevcut kullanıcı ilk açılışta 15 kutlamayla karşılaşır.
/// </summary>
public class AchievementService
{
    public sealed record Def(string Key, string Category, string Title, string Description, string Icon, string Unit,
        double[] Tiers, Func<Stats, double> Value, Func<Stats, string?>? Hint = null);

    public sealed class Stats
    {
        public int Meals;
        public int LongestMealStreak;
        public int ProteinDays;
        public int CalorieDays;
        public int EarlyBreakfasts;
        public int Workouts;
        public double VolumeKg;
        public int PersonalRecords;
        public int BestWeek;
        public int Templates;
        public int WeighIns;
        public double? TargetProgress; // 0..1, hedef kiloya kat edilen yol
        public bool HasTarget;
        public int CheckIns;
        public int WaterDays;
        public int AllHabitDays;
        public int FullDays;
    }

    /// <summary>Kategori sırası arayüzdeki filtre sırasıdır.</summary>
    public static readonly Def[] Defs =
    {
        // Beslenme
        new("ilk-lokma", "nutrition", "İlk Lokma", "İlk öğününü kaydet.", "ForkKnife", "öğün", new[] { 1.0 }, s => s.Meals),
        new("ates", "nutrition", "Ateşi Yak", "Üst üste {0} gün öğün gir.", "Fire", "gün", new[] { 3.0, 7, 30 }, s => s.LongestMealStreak),
        new("protein-avcisi", "nutrition", "Protein Avcısı", "Protein hedefini {0} gün tuttur.", "Egg", "gün", new[] { 1.0, 10, 50 }, s => s.ProteinDays),
        new("dengeli", "nutrition", "Tam Ayar", "Kalori hedefine ±%10 yakın {0} gün geçir.", "Target", "gün", new[] { 3.0, 14, 60 }, s => s.CalorieDays),
        new("erkenci", "nutrition", "Erkenci Kuş", "Saat 09:00'dan önce {0} kez kahvaltı gir.", "SunHorizon", "kahvaltı", new[] { 5.0, 30 }, s => s.EarlyBreakfasts),

        // Antrenman
        new("ilk-ter", "workout", "İlk Ter", "İlk antrenmanını tamamla.", "Barbell", "antrenman", new[] { 1.0 }, s => s.Workouts),
        new("duzenli", "workout", "Demir Disiplin", "{0} antrenman tamamla.", "CalendarCheck", "antrenman", new[] { 10.0, 50, 150 }, s => s.Workouts),
        new("ton", "workout", "Ton Kaldıran", "Toplam {0} kg hacim kaldır.", "Mountains", "kg", new[] { 10_000.0, 100_000, 1_000_000 }, s => s.VolumeKg),
        new("rekor", "workout", "Rekor Kıran", "{0} kişisel rekor kır.", "Trophy", "rekor", new[] { 1.0, 10, 40 }, s => s.PersonalRecords),
        new("hafta-savascisi", "workout", "Hafta Savaşçısı", "7 gün içinde {0} antrenman yap.", "Lightning", "antrenman", new[] { 4.0, 6 }, s => s.BestWeek),
        new("sablon", "workout", "Plan Yapan", "İlk antrenman şablonunu kaydet.", "Bookmarks", "şablon", new[] { 1.0 }, s => s.Templates),

        // Kilo
        new("tarti", "weight", "Tartıya Çık", "{0} kez tartıl.", "Scales", "tartı", new[] { 1.0, 30, 100 }, s => s.WeighIns),
        new("hedef-yolu", "weight", "Yolun Yarısı", "Hedef kiloya giden yolun yarısını geç.", "Flag", "%", new[] { 50.0 },
            s => Math.Round((s.TargetProgress ?? 0) * 100), s => s.HasTarget ? null : "Profil → Hedefler'den hedef kilo gir."),
        new("hedefte", "weight", "Hedefte", "Hedef kilona ulaş.", "Crown", "%", new[] { 100.0 },
            s => Math.Round((s.TargetProgress ?? 0) * 100), s => s.HasTarget ? null : "Profil → Hedefler'den hedef kilo gir."),

        // Durum ve alışkanlık
        new("kendini-dinle", "mind", "Kendini Dinle", "{0} kez hissiyatını kaydet.", "Smiley", "kayıt", new[] { 1.0, 10, 50 }, s => s.CheckIns),
        new("su-gibi", "mind", "Su Gibi", "Su hedefini {0} gün tamamla.", "Drop", "gün", new[] { 1.0, 7, 30 }, s => s.WaterDays),
        new("rutin", "mind", "Rutin", "Bütün alışkanlıklarını {0} gün eksiksiz bitir.", "Sparkle", "gün", new[] { 1.0, 7, 30 }, s => s.AllHabitDays),

        // Genel
        new("tam-gun", "general", "Tam Gün", "Aynı gün öğün, antrenman, tartı ve hissiyat gir — {0} kez.", "Star", "gün", new[] { 1.0, 10, 30 }, s => s.FullDays),
    };

    /// <summary>Kullanıcının başarımları ilk kez değerlendirildi işareti — arayüze gitmez.</summary>
    public const string BaselineKey = "_baseline";

    private readonly AppDbContext _db;
    public AchievementService(AppDbContext db) => _db = db;

    public async Task<List<AchievementView>> EvaluateAsync(CancellationToken ct)
    {
        var stats = await ComputeAsync(ct);
        var unlocked = await _db.UserAchievements.ToListAsync(ct);
        var now = DateTime.Now;
        // "İlk değerlendirme" başarım sayısına değil bu işarete bakar: verisi olmayan yeni kullanıcının
        // ilk değerlendirmesi hiçbir şey açmaz, ama kazandığı ilk başarım yine de kutlanmalı.
        var firstRun = !unlocked.Any(u => u.Key == BaselineKey);
        if (firstRun)
        {
            var baseline = new UserAchievement { Id = Guid.NewGuid(), Key = BaselineKey, Tier = 0, UnlockedAt = now, SeenAt = now };
            _db.UserAchievements.Add(baseline);
            unlocked.Add(baseline);
        }

        foreach (var def in Defs)
        {
            var value = def.Value(stats);
            for (var tier = 1; tier <= def.Tiers.Length; tier++)
            {
                if (value < def.Tiers[tier - 1]) break;
                if (unlocked.Any(u => u.Key == def.Key && u.Tier == tier)) continue;
                var row = new UserAchievement
                {
                    Id = Guid.NewGuid(), Key = def.Key, Tier = tier, UnlockedAt = now,
                    SeenAt = firstRun ? now : null,
                };
                _db.UserAchievements.Add(row);
                unlocked.Add(row);
            }
        }
        if (_db.ChangeTracker.HasChanges())
        {
            try { await _db.SaveChangesAsync(ct); }
            catch (DbUpdateException) { _db.ChangeTracker.Clear(); unlocked = await _db.UserAchievements.ToListAsync(ct); } // eşzamanlı iki istek
        }

        return Defs.Select(def =>
        {
            var value = def.Value(stats);
            var mine = unlocked.Where(u => u.Key == def.Key).ToList();
            var level = mine.Count == 0 ? 0 : mine.Max(u => u.Tier);
            return new AchievementView(
                def.Key, def.Category, def.Title,
                string.Format(def.Description, Format(level < def.Tiers.Length ? def.Tiers[level] : def.Tiers[^1])),
                def.Icon, def.Unit, Math.Round(value, 1), level, def.Tiers.Length,
                level < def.Tiers.Length ? def.Tiers[level] : null,
                mine.Any(u => u.SeenAt == null),
                level == 0 ? def.Hint?.Invoke(stats) : null,
                def.Tiers.Select((t, i) => new AchievementTierView(i + 1, t, mine.FirstOrDefault(u => u.Tier == i + 1)?.UnlockedAt)).ToList());
        }).ToList();
    }

    public async Task MarkSeenAsync(CancellationToken ct)
    {
        var now = DateTime.Now;
        await _db.UserAchievements.Where(u => u.SeenAt == null).ExecuteUpdateAsync(s => s.SetProperty(u => u.SeenAt, now), ct);
    }

    private static string Format(double v) => v >= 1000 ? v.ToString("N0", new System.Globalization.CultureInfo("tr-TR")) : v.ToString("0");

    internal async Task<Stats> ComputeAsync(CancellationToken ct)
    {
        var s = new Stats();

        // --- beslenme ---
        var goals = await _db.UserGoals.AsNoTracking().FirstOrDefaultAsync(ct);
        var meals = await _db.MealEntries.AsNoTracking()
            .Select(m => new { m.LoggedAt, m.Calories, m.Protein, m.MealType }).ToListAsync(ct);
        s.Meals = meals.Count;
        var mealDays = meals.GroupBy(m => m.LoggedAt.Date)
            .Select(g => new { Day = g.Key, Cal = g.Sum(x => x.Calories), Pro = g.Sum(x => x.Protein) }).ToList();
        s.LongestMealStreak = LongestRun(mealDays.Select(d => d.Day));
        if (goals is { ProteinGoal: > 0 }) s.ProteinDays = mealDays.Count(d => d.Pro >= goals.ProteinGoal * 0.9);
        if (goals is { CalorieGoal: > 0 }) s.CalorieDays = mealDays.Count(d => d.Cal >= goals.CalorieGoal * 0.9 && d.Cal <= goals.CalorieGoal * 1.1);
        s.EarlyBreakfasts = meals.Where(m => m.MealType == MealType.Breakfast && m.LoggedAt.Hour < 9)
            .Select(m => m.LoggedAt.Date).Distinct().Count();

        // --- antrenman: yalnız tamamlanan setler sayılır ---
        var sets = await _db.ExerciseSets.AsNoTracking().Where(x => x.IsCompleted)
            .Select(x => new { x.WeightKg, x.Reps, x.Exercise!.Name, x.Exercise.WorkoutSessionId, x.Exercise.WorkoutSession!.LoggedAt })
            .ToListAsync(ct);
        var sessionDays = sets.GroupBy(x => x.WorkoutSessionId).Select(g => g.First().LoggedAt).ToList();
        s.Workouts = sessionDays.Count;
        s.VolumeKg = sets.Sum(x => x.WeightKg * x.Reps);
        s.BestWeek = sessionDays.Count == 0 ? 0
            : sessionDays.Max(d => sessionDays.Count(o => o.Date >= d.Date && o.Date < d.Date.AddDays(7)));

        // Kişisel rekor: bir hareketin seanstaki en ağır seti, o hareketin önceki tüm seanslarından ağırsa.
        // Hareketin ilk yapıldığı seans rekor sayılmaz (kıyaslanacak geçmiş yok).
        foreach (var byExercise in sets.Where(x => x.WeightKg > 0).GroupBy(x => x.Name.Trim().ToLowerInvariant()))
        {
            double? best = null;
            foreach (var session in byExercise.GroupBy(x => x.WorkoutSessionId).OrderBy(g => g.First().LoggedAt))
            {
                var top = session.Max(x => x.WeightKg);
                if (best is not null && top > best) s.PersonalRecords++;
                best = best is null ? top : Math.Max(best.Value, top);
            }
        }
        s.Templates = await _db.WorkoutTemplates.CountAsync(ct);

        // --- kilo ---
        var weights = await _db.WeightLogs.AsNoTracking().OrderBy(w => w.LoggedAt).Select(w => w.WeightKg).ToListAsync(ct);
        s.WeighIns = weights.Count;
        var target = (await _db.Profiles.AsNoTracking().FirstOrDefaultAsync(ct))?.TargetWeightKg;
        s.HasTarget = target is > 0;
        if (s.HasTarget && weights.Count > 0)
        {
            var start = weights[0];
            var current = weights[^1];
            var total = start - target!.Value;
            // Başlangıç zaten hedefteyse yol yok; hedefe varılmış sayılır.
            s.TargetProgress = Math.Abs(total) < 0.05 ? 1 : Math.Clamp((start - current) / total, 0, 1);
        }

        // --- durum ve alışkanlık ---
        s.CheckIns = await _db.CheckIns.CountAsync(ct);
        var habits = await _db.Habits.AsNoTracking().ToListAsync(ct);
        var logs = await _db.HabitLogs.AsNoTracking().ToListAsync(ct);
        var waterIds = habits.Where(h => h.Icon == "water").Select(h => h.Id).ToHashSet();
        var targetOf = habits.ToDictionary(h => h.Id, h => h.Target);
        s.WaterDays = logs.Where(l => waterIds.Contains(l.HabitId) && l.Count >= targetOf[l.HabitId]).Select(l => l.Day).Distinct().Count();
        foreach (var dayLogs in logs.GroupBy(l => l.Day))
        {
            // O gün var olan (oluşturulmuş, arşivlenmemiş ya da o gün kaydı olan) her alışkanlık tamam mı?
            var day = dayLogs.Key;
            var active = habits.Where(h => h.CreatedAt.Date <= day && (!h.IsArchived || dayLogs.Any(l => l.HabitId == h.Id))).ToList();
            if (active.Count > 0 && active.All(h => dayLogs.Any(l => l.HabitId == h.Id && l.Count >= h.Target)))
                s.AllHabitDays++;
        }

        // --- tam gün ---
        var weighDays = (await _db.WeightLogs.AsNoTracking().Select(w => w.LoggedAt).ToListAsync(ct)).Select(d => d.Date).ToHashSet();
        var checkDays = (await _db.CheckIns.AsNoTracking().Select(c => c.LoggedAt).ToListAsync(ct)).Select(d => d.Date).ToHashSet();
        var workoutDays = sessionDays.Select(d => d.Date).ToHashSet();
        s.FullDays = mealDays.Count(d => workoutDays.Contains(d.Day) && weighDays.Contains(d.Day) && checkDays.Contains(d.Day));

        return s;
    }

    /// <summary>Ardışık günlerin en uzun serisi.</summary>
    private static int LongestRun(IEnumerable<DateTime> days)
    {
        var sorted = days.Select(d => d.Date).Distinct().OrderBy(d => d).ToList();
        int best = 0, run = 0;
        for (var i = 0; i < sorted.Count; i++)
        {
            run = i > 0 && sorted[i] == sorted[i - 1].AddDays(1) ? run + 1 : 1;
            best = Math.Max(best, run);
        }
        return best;
    }
}
