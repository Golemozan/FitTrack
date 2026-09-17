using System.Security.Claims;

namespace FitTrack.API.Security;

/// <summary>
/// İsteğin kime ait olduğu. Web isteğinde doğrulanmış oturum çerezinden okunur.
/// Arka plan işleri (Telegram, günlük koç) HTTP bağlamı olmadığı için kullanıcıyı
/// <see cref="ActAs"/> ile kendi kapsamlarında açıkça belirler.
/// Kimlik yoksa <see cref="Id"/> null'dır ve veritabanı filtresi hiçbir satır döndürmez.
/// </summary>
public sealed class CurrentUser
{
    private readonly IHttpContextAccessor _http;
    private Guid? _actingAs;

    public CurrentUser(IHttpContextAccessor http) => _http = http;

    public Guid? Id => _actingAs ?? FromPrincipal(_http.HttpContext?.User);

    /// <summary>Yalnız arka plan işleri için. Bir kez atanır, değiştirilemez.</summary>
    public void ActAs(Guid userId)
    {
        if (_http.HttpContext is not null)
            throw new InvalidOperationException("ActAs yalnız HTTP dışı kapsamda kullanılabilir.");
        if (_actingAs is not null && _actingAs != userId)
            throw new InvalidOperationException("Kapsamın kullanıcısı zaten belirlendi.");
        _actingAs = userId;
    }

    public Guid Require() => Id ?? throw new UnauthorizedAccessException("Kimliği doğrulanmış kullanıcı yok.");

    public static Guid? FromPrincipal(ClaimsPrincipal? principal)
    {
        if (principal?.Identity?.IsAuthenticated != true) return null;
        return Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    }
}
