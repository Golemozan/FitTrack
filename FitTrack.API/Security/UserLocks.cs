using System.Collections.Concurrent;

namespace FitTrack.API.Security;

/// <summary>
/// "Yoksa oluştur" yapan GET uçları (varsayılan su alışkanlığı, hedef, profil) aynı anda gelen iki
/// istekte iki satır açıyordu. Kullanıcı başına tek kilit; uygulama tek örnek çalıştığı için yeterli.
/// </summary>
public static class UserLocks
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new();

    public static async Task<IDisposable> AcquireAsync(Guid? userId, string scope, CancellationToken ct = default)
    {
        var gate = Locks.GetOrAdd($"{scope}:{userId}", _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        return new Releaser(gate);
    }

    private sealed class Releaser(SemaphoreSlim gate) : IDisposable
    {
        public void Dispose() => gate.Release();
    }
}
