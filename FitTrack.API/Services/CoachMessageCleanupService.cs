using FitTrack.API.Data;
using Microsoft.EntityFrameworkCore;

namespace FitTrack.API.Services;

/// <summary>
/// Sistem istemi ve arayüz "sohbet geçmişi 3 günde silinir" diyor. Yalnız açılışta silmek bunu tutmaz:
/// sağlık sohbeti yeniden başlatma olmadan haftalarca kalırdı. Açılışın yanında saatte bir de çalışır.
/// </summary>
public class CoachMessageCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scope;
    private readonly ILogger<CoachMessageCleanupService> _log;

    public CoachMessageCleanupService(IServiceScopeFactory scope, ILogger<CoachMessageCleanupService> log)
    {
        _scope = scope;
        _log = log;
    }

    internal TimeSpan Interval { get; set; } = TimeSpan.FromHours(1);

    public static int Run(AppDbContext db) =>
        db.Database.ExecuteSqlRaw("DELETE FROM CoachMessages WHERE CreatedAt < datetime('now', '-3 days');");

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(Interval);
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                try
                {
                    using var scope = _scope.CreateScope();
                    Run(scope.ServiceProvider.GetRequiredService<AppDbContext>());
                }
                catch (Exception ex) { _log.LogError(ex, "Koç mesajı temizliği başarısız."); }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { /* kapanış */ }
    }
}
