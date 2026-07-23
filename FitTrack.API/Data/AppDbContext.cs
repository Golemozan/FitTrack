using FitTrack.API.Models;
using Microsoft.EntityFrameworkCore;

namespace FitTrack.API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

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

    // Fixed identifiers so the seed is deterministic across migrations.
    public static readonly Guid DefaultGoalsId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid DefaultProfileId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
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

        modelBuilder.Entity<UserGoals>().HasData(new UserGoals
        {
            Id = DefaultGoalsId,
            CalorieGoal = 2500,
            ProteinGoal = 180,
            CarbGoal = 250,
            FatGoal = 70,
            UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
    }
}
