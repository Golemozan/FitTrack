using System.Threading.RateLimiting;

namespace FitTrack.API.Security;

/// <summary>
/// Sunucu geneli kayıt tavanı (saatte N). IP başına sınır, IP taklit edilebildiği ya da çok adresten
/// gelindiği an işe yaramaz; bu tavan hesap açarak diski doldurmayı her durumda sınırlar.
/// Kişisel/koç kurulumunda saatte 30 yeni hesap gerçek kullanımın çok üstünde.
/// </summary>
public sealed class RegistrationGate(int perHour)
{
    private readonly FixedWindowRateLimiter _limiter = new(new FixedWindowRateLimiterOptions
    {
        PermitLimit = Math.Max(1, perHour), Window = TimeSpan.FromHours(1), QueueLimit = 0,
    });

    public bool TryEnter()
    {
        using var lease = _limiter.AttemptAcquire();
        return lease.IsAcquired;
    }
}
