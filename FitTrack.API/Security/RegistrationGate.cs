using System.Threading.RateLimiting;

namespace FitTrack.API.Security;

/// <summary>
/// Sunucu geneli kayıt tavanı (saatte N). IP başına sınır, IP taklit edilebildiği ya da çok adresten
/// gelindiği an işe yaramaz; bu tavan hesap açarak diski doldurmayı her durumda sınırlar.
/// Kişisel/koç kurulumunda saatte 30 yeni hesap gerçek kullanımın çok üstünde.
/// IP başına saatlik kova önce bakılır: tek adres sunucu geneli kotayı tüketip başkalarının kaydını kapatamaz.
/// Yalnız hesap açmaya gelmiş istek sayılır; hatalı form ve var olan e-posta kotadan düşmez.
/// </summary>
public sealed class RegistrationGate(int perHour, int perIpPerHour)
{
    private readonly FixedWindowRateLimiter _limiter = new(new FixedWindowRateLimiterOptions
    {
        PermitLimit = Math.Max(1, perHour), Window = TimeSpan.FromHours(1), QueueLimit = 0,
    });

    private readonly PartitionedRateLimiter<string> _perIp = PartitionedRateLimiter.Create<string, string>(ip =>
        RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = Math.Max(1, perIpPerHour), Window = TimeSpan.FromHours(1), QueueLimit = 0,
        }));

    public bool TryEnter(string clientIp)
    {
        using var ipLease = _perIp.AttemptAcquire(clientIp);
        if (!ipLease.IsAcquired) return false;
        using var lease = _limiter.AttemptAcquire();
        return lease.IsAcquired;
    }
}
