using System.Linq.Expressions;
using System.Reflection;
using FitTrack.API.Models;
using FitTrack.API.Security;
using Microsoft.EntityFrameworkCore;

namespace FitTrack.API.Data;

public class AppDbContext : DbContext
{
    private readonly CurrentUser? _currentUser;

    public AppDbContext(DbContextOptions<AppDbContext> options, CurrentUser currentUser) : base(options)
    {
        _currentUser = currentUser;
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<UserApiKey> UserApiKeys => Set<UserApiKey>();
    public DbSet<MealEntry> MealEntries => Set<MealEntry>();
    public DbSet<WorkoutSession> WorkoutSessions => Set<WorkoutSession>();
    public DbSet<Exercise> Exercises => Set<Exercise>();
    public DbSet<ExerciseSet> ExerciseSets => Set<ExerciseSet>();
    public DbSet<WeightLog> WeightLogs => Set<WeightLog>();
    public DbSet<UserGoals> UserGoals => Set<UserGoals>();
    public DbSet<CheckIn> CheckIns => Set<CheckIn>();
    public DbSet<Profile> Profiles => Set<Profile>();
    public DbSet<CoachMessageRecord> CoachMessages => Set<CoachMessageRecord>();
    public DbSet<CoachNote> CoachNotes => Set<CoachNote>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<Habit> Habits => Set<Habit>();
    public DbSet<HabitLog> HabitLogs => Set<HabitLog>();
    public DbSet<WorkoutTemplate> WorkoutTemplates => Set<WorkoutTemplate>();
    public DbSet<TemplateExercise> TemplateExercises => Set<TemplateExercise>();
    public DbSet<UserAchievement> UserAchievements => Set<UserAchievement>();
    public DbSet<HomeLayout> HomeLayouts => Set<HomeLayout>();
    public DbSet<RevokedSession> RevokedSessions => Set<RevokedSession>();

    /// <summary>
    /// Sorgu filtresinin okuduğu değer. EF bunu her sorguda bu bağlamın örneğinden parametre
    /// olarak alır. Kimlik yoksa hiçbir gerçek kullanıcıyla eşleşmeyen boş Guid döner →
    /// kimliksiz sorgu boş sonuç verir, başkasının verisini değil.
    /// </summary>
    private Guid FilterUserId => _currentUser?.Id ?? Guid.Empty;

    /// <summary>Tek-kullanıcı döneminden kalan, henüz sahiplenilmemiş satırların işareti.</summary>
    public static readonly Guid LegacyOwner = Guid.Empty;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();
            e.HasIndex(u => u.TelegramChatId).IsUnique().HasFilter("\"TelegramChatId\" IS NOT NULL");
        });

        modelBuilder.Entity<WorkoutSession>()
            .HasMany(w => w.Exercises)
            .WithOne(e => e.WorkoutSession!)
            .HasForeignKey(e => e.WorkoutSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Exercise>()
            .HasMany(e => e.Sets)
            .WithOne(s => s.Exercise!)
            .HasForeignKey(s => s.ExerciseId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<WorkoutTemplate>()
            .HasMany(t => t.Exercises)
            .WithOne(e => e.WorkoutTemplate!)
            .HasForeignKey(e => e.WorkoutTemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<HomeLayout>().HasIndex(h => h.UserId).IsUnique().HasDatabaseName("IX_HomeLayouts_UserId_Unique");

        // Kullanıcı başına her kademe bir kez açılır.
        modelBuilder.Entity<UserAchievement>().HasIndex(a => new { a.UserId, a.Key, a.Tier }).IsUnique();

        // Alışkanlık başına gün başına tek satır; sayaç güncellenir, yeni satır açılmaz.
        modelBuilder.Entity<HabitLog>().HasIndex(l => new { l.HabitId, l.Day }).IsUnique();
        modelBuilder.Entity<RevokedSession>().HasKey(r => r.Sid);

        // Her kullanıcıya ait tabloya aynı filtre: yalnız oturumdaki kullanıcının satırları görünür.
        foreach (var type in modelBuilder.Model.GetEntityTypes()
                     .Where(t => typeof(IUserOwned).IsAssignableFrom(t.ClrType)).ToList())
        {
            modelBuilder.Entity(type.ClrType).HasIndex(nameof(IUserOwned.UserId));
            modelBuilder.Entity(type.ClrType).HasQueryFilter(BuildOwnerFilter(type.ClrType));
        }
    }

    private LambdaExpression BuildOwnerFilter(Type clrType)
    {
        var param = Expression.Parameter(clrType, "e");
        var owner = Expression.Property(param, nameof(IUserOwned.UserId));
        var current = Expression.Property(Expression.Constant(this),
            typeof(AppDbContext).GetProperty(nameof(FilterUserId), BindingFlags.Instance | BindingFlags.NonPublic)!);
        return Expression.Lambda(Expression.Equal(owner, current), param);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnforceOwnership();
        EnforceRules();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken ct = default)
    {
        EnforceOwnership();
        EnforceRules();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, ct);
    }

    /// <summary>
    /// Sorgu filtresi okumayı korur; bu da yazmayı. Her eklenen satır oturumdaki kullanıcıya
    /// damgalanır, başkasına ait bir satırı değiştirmek/silmek ya da sahibini değiştirmek istisna fırlatır.
    /// Alt kayıtlar (hareket, set) ayrıca üst kaydın aynı kullanıcıya ait olduğunu kanıtlamak zorunda.
    /// </summary>
    private void EnforceOwnership()
    {
        var entries = ChangeTracker.Entries<IUserOwned>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();
        if (entries.Count == 0) return;

        var userId = _currentUser?.Id
            ?? throw new UnauthorizedAccessException("Kullanıcıya ait veri kimliksiz yazılamaz.");

        foreach (var entry in entries)
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    if (entry.Entity.UserId != Guid.Empty && entry.Entity.UserId != userId)
                        throw new UnauthorizedAccessException("Başka bir kullanıcı adına kayıt eklenemez.");
                    entry.Entity.UserId = userId;
                    break;

                case EntityState.Modified:
                case EntityState.Deleted:
                    var prop = entry.Property<Guid>(nameof(IUserOwned.UserId));
                    if (prop.OriginalValue != userId || prop.CurrentValue != userId)
                        throw new UnauthorizedAccessException("Başka bir kullanıcının kaydı değiştirilemez.");
                    break;
            }
        }

        foreach (var entry in entries.Where(e => e.State == EntityState.Added))
        {
            switch (entry.Entity)
            {
                case Exercise ex when !OwnsSession(ex.WorkoutSessionId, userId):
                    throw new UnauthorizedAccessException("Antrenman seansı bu kullanıcıya ait değil.");
                case HabitLog log when !Owns<Habit>(log.HabitId, userId):
                    throw new UnauthorizedAccessException("Alışkanlık bu kullanıcıya ait değil.");
                case TemplateExercise te when !Owns<WorkoutTemplate>(te.WorkoutTemplateId, userId):
                    throw new UnauthorizedAccessException("Şablon bu kullanıcıya ait değil.");
                case ExerciseSet set when !OwnsExercise(set.ExerciseId, userId):
                    throw new UnauthorizedAccessException("Hareket bu kullanıcıya ait değil.");
            }
        }
    }

    /// <summary>
    /// <see cref="DataRules"/>: eklenen/değişen her satırın sınırı ve eklenenlerin kotası. İhlalde bekleyen
    /// değişiklikler geri alınır — aynı kapsamda sonraki bir kayıt (ör. koçun bir sonraki aracı) bozuk
    /// satırı yeniden yazmaya çalışmasın.
    /// </summary>
    private void EnforceRules()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified)
            .ToList();
        foreach (var entry in entries)
        {
            var error = DataRules.Check(entry.Entity)
                ?? (entry.State == EntityState.Added ? DataRules.Quota(this, entry.Entity) : null);
            if (error is null) continue;

            foreach (var pending in ChangeTracker.Entries()
                         .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToList())
            {
                if (pending.State == EntityState.Added) { pending.State = EntityState.Detached; continue; }
                pending.CurrentValues.SetValues(pending.OriginalValues);
                pending.State = EntityState.Unchanged;
            }
            throw new DataRuleException(error);
        }
    }

    /// <summary>Üst kayıt bu kullanıcının mı? Önce izlenen değişiklikler, sonra filtreli sorgu (yalnız kendi satırları).</summary>
    private bool Owns<T>(Guid id, Guid userId) where T : class, IUserOwned =>
        ChangeTracker.Entries<T>().Any(e => (Guid)e.Property("Id").CurrentValue! == id && e.Entity.UserId == userId)
        || Set<T>().Any(x => EF.Property<Guid>(x, "Id") == id);

    private bool OwnsSession(Guid sessionId, Guid userId) =>
        ChangeTracker.Entries<WorkoutSession>().Any(e => e.Entity.Id == sessionId && e.Entity.UserId == userId)
        || WorkoutSessions.Any(s => s.Id == sessionId); // filtreli: yalnız kendi seansı

    private bool OwnsExercise(Guid exerciseId, Guid userId) =>
        ChangeTracker.Entries<Exercise>().Any(e => e.Entity.Id == exerciseId && e.Entity.UserId == userId)
        || Exercises.Any(x => x.Id == exerciseId);
}
