using FitTrack.API.Models;
using Microsoft.EntityFrameworkCore;

namespace FitTrack.API.Data;

/// <summary>Kural ihlali: istemciye 400 ve bu mesaj döner (bkz. Program.cs hata işleyicisi).</summary>
public sealed class DataRuleException(string message) : Exception(message);

/// <summary>
/// Yazılan her satırın sınırları — uzunluk, sayı aralığı, tarih penceresi ve günlük kota.
/// Uç uç değil burada: aynı kayıt hem API'den hem AI koçun araçlarından yazılıyor, kural tek yerde
/// durursa hiçbir yol atlayamaz. Amaç tek kullanıcının diski doldurmasını ve kendi özetini Infinity
/// gibi değerlerle bozmasını engellemek; gerçekçi kullanımın çok üstünde tutuldu.
/// </summary>
public static class DataRules
{
    public const int MaxDaysBack = 3650;

    // Kullanıcı başına toplam satır tavanı: günlük kota × 10 yıllık pencere diski doldurmaya yetiyordu.
    // Yalnız ekleme durur; eldeki kayıt silinmez, düzenlenebilir. Değerler gerçek kullanımın çok üstünde
    // (10 yılda günde 13 öğün, 2-3 antrenman, 5 durum; antrenman başına ~5 hareket, hareket başına ~5 set).
    public const int MaxMealsTotal = 50_000;
    public const int MaxWorkoutsTotal = 10_000;
    public const int MaxCheckInsTotal = 20_000;
    public const int MaxExercisesTotal = 50_000;
    public const int MaxSetsTotal = 250_000;
    public const int MaxHabitsTotal = 200; // arşiv dahil

    public static readonly string[] CheckInContexts = { "general", "pre-workout", "post-workout" };
    private static readonly string[] NoteCategories = { "injury", "preference", "goal", "fact" };

    public static string? Check(object entity) => entity switch
    {
        MealEntry m => Text(m.FoodName, 1, 100, "Besin adı")
            ?? Num(m.Grams, 0, 5000, "Gram") ?? Num(m.Calories, 0, 10000, "Kalori")
            ?? Num(m.Protein, 0, 1000, "Protein") ?? Num(m.Carbs, 0, 1000, "Karbonhidrat") ?? Num(m.Fat, 0, 1000, "Yağ")
            ?? (Enum.IsDefined(m.MealType) ? null : "Geçersiz öğün.")
            ?? Date(m.LoggedAt),
        WorkoutSession s => Text(s.Name, 1, 80, "Antrenman adı") ?? Date(s.LoggedAt),
        Exercise e => Text(e.Name, 1, 80, "Hareket adı") ?? Text(e.MuscleGroup, 0, 40, "Kas grubu"),
        ExerciseSet s => Num(s.SetNumber, 1, 50, "Set numarası") ?? Num(s.WeightKg, -500, 1000, "Ağırlık")
            ?? Num(s.Reps, 0, 1000, "Tekrar"),
        WeightLog w => Num(w.WeightKg, 1, 700, "Kilo") ?? Text(w.Notes, 0, 500, "Not") ?? Date(w.LoggedAt),
        CheckIn c => Text(c.Note, 0, 500, "Not")
            ?? (CheckInContexts.Contains(c.Context) ? null : "Geçersiz durum bağlamı.") ?? Date(c.LoggedAt),
        UserGoals g => Num(g.CalorieGoal, 0, 20000, "Kalori hedefi") ?? Num(g.ProteinGoal, 0, 2000, "Protein hedefi")
            ?? Num(g.CarbGoal, 0, 2000, "Karbonhidrat hedefi") ?? Num(g.FatGoal, 0, 2000, "Yağ hedefi"),
        Profile p => (p.HeightCm is { } h ? Num(h, 50, 260, "Boy") : null)
            ?? (p.TargetWeightKg is { } t ? Num(t, 20, 500, "Hedef kilo") : null),
        CoachNote n => Text(n.Content, 1, 1000, "Not") ?? (NoteCategories.Contains(n.Category) ? null : "Geçersiz not türü."),
        CoachMessageRecord r => Text(r.Content, 0, 20000, "Mesaj"),
        _ => null,
    };

    /// <summary>Günlük / üst kayıt başına satır sınırı. Eklenen satır sayılırken izlenen değişiklikler de hesaba katılır.</summary>
    public static string? Quota(AppDbContext db, object entity)
    {
        switch (entity)
        {
            case MealEntry m:
                return Over(db.MealEntries, x => x.LoggedAt >= m.LoggedAt.Date && x.LoggedAt < m.LoggedAt.Date.AddDays(1),
                    db.ChangeTracker.Entries<MealEntry>().Count(e => e.State == EntityState.Added && e.Entity.LoggedAt.Date == m.LoggedAt.Date),
                    100, "Bir güne en fazla 100 besin girilebilir.")
                    ?? Total(db, db.MealEntries, MaxMealsTotal, $"Toplam {MaxMealsTotal} besin sınırına ulaştın. Yeni kayıt için eski kayıtları sil.");
            case WorkoutSession s:
                return Over(db.WorkoutSessions, x => x.LoggedAt >= s.LoggedAt.Date && x.LoggedAt < s.LoggedAt.Date.AddDays(1),
                    db.ChangeTracker.Entries<WorkoutSession>().Count(e => e.State == EntityState.Added && e.Entity.LoggedAt.Date == s.LoggedAt.Date),
                    10, "Bir güne en fazla 10 antrenman açılabilir.")
                    ?? Total(db, db.WorkoutSessions, MaxWorkoutsTotal, $"Toplam {MaxWorkoutsTotal} antrenman sınırına ulaştın. Yeni kayıt için eski kayıtları sil.");
            case CheckIn c:
                return Over(db.CheckIns, x => x.LoggedAt >= c.LoggedAt.Date && x.LoggedAt < c.LoggedAt.Date.AddDays(1),
                    db.ChangeTracker.Entries<CheckIn>().Count(e => e.State == EntityState.Added && e.Entity.LoggedAt.Date == c.LoggedAt.Date),
                    30, "Bir güne en fazla 30 durum kaydı girilebilir.")
                    ?? Total(db, db.CheckIns, MaxCheckInsTotal, $"Toplam {MaxCheckInsTotal} durum kaydı sınırına ulaştın. Yeni kayıt için eski kayıtları sil.");
            case Exercise ex:
                return Over(db.Exercises, x => x.WorkoutSessionId == ex.WorkoutSessionId,
                    db.ChangeTracker.Entries<Exercise>().Count(e => e.State == EntityState.Added && e.Entity.WorkoutSessionId == ex.WorkoutSessionId),
                    30, "Bir antrenmana en fazla 30 hareket eklenebilir.")
                    ?? Total(db, db.Exercises, MaxExercisesTotal, $"Toplam {MaxExercisesTotal} hareket sınırına ulaştın. Yeni kayıt için eski antrenmanları sil.");
            case ExerciseSet set:
                return Over(db.ExerciseSets, x => x.ExerciseId == set.ExerciseId,
                    db.ChangeTracker.Entries<ExerciseSet>().Count(e => e.State == EntityState.Added && e.Entity.ExerciseId == set.ExerciseId),
                    30, "Bir harekete en fazla 30 set eklenebilir.")
                    ?? Total(db, db.ExerciseSets, MaxSetsTotal, $"Toplam {MaxSetsTotal} set sınırına ulaştın. Yeni kayıt için eski antrenmanları sil.");
            case Habit:
                return Total(db, db.Habits, MaxHabitsTotal, $"Arşivdekiler dahil en fazla {MaxHabitsTotal} alışkanlık açılabilir.");
            case CoachNote:
                return Over(db.CoachNotes, _ => true,
                    db.ChangeTracker.Entries<CoachNote>().Count(e => e.State == EntityState.Added),
                    200, "En fazla 200 koç notu tutulabilir.");
            default:
                return null;
        }
    }

    /// <summary>Sorgu parametresindeki gün: makul pencerenin dışındaysa null (çağıran 400 döner).</summary>
    public static bool InWindow(DateTime day) =>
        day >= DateTime.Now.Date.AddDays(-MaxDaysBack) && day <= DateTime.Now.Date.AddDays(1);

    // Kayıt sorgusu global filtreyle yalnız oturumdaki kullanıcının satırlarını sayar.
    private static string? Over<T>(IQueryable<T> set, System.Linq.Expressions.Expression<Func<T, bool>> scope,
        int pending, int max, string error) =>
        set.Count(scope) + pending > max ? error : null;

    private static string? Total<T>(AppDbContext db, IQueryable<T> set, int max, string error) where T : class =>
        set.Count() + db.ChangeTracker.Entries<T>().Count(e => e.State == EntityState.Added) > max ? error : null;

    private static string? Text(string? value, int min, int max, string field)
    {
        var len = value?.Trim().Length ?? 0;
        if (len < min) return $"{field} boş olamaz.";
        return (value?.Length ?? 0) > max ? $"{field} en fazla {max} karakter olabilir." : null;
    }

    private static string? Num(double value, double min, double max, string field) =>
        double.IsFinite(value) && value >= min && value <= max ? null : $"{field} {min}-{max} arasında olmalı.";

    // Geçmişe en fazla ~10 yıl, ileriye saat dilimi payı kadar.
    private static string? Date(DateTime value) =>
        InWindow(value.Date) ? null : "Tarih geçersiz: gelecek ya da çok eski bir gün olamaz.";
}
