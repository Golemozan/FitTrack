using Microsoft.AspNetCore.DataProtection;

namespace FitTrack.API.Security;

/// <summary>
/// Başarılı girişten sonra tarayıcıya bırakılan "bu cihaz bu hesaba daha önce girdi" işareti.
/// Hesap geneli kilit (dağıtık deneme) bu cihazı durdurmaz: kilit, e-postayı bilen birinin sahibi
/// dışarıda bırakmasına dönüşmesin. İşaret şifreli ve imzalı (DataProtection) — taklit edilemez,
/// kendi başına oturum açmaz; parola yine doğrulanır.
/// </summary>
public sealed class KnownDevice(IDataProtectionProvider provider, IHostEnvironment env)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(180);
    private readonly ITimeLimitedDataProtector _protector =
        provider.CreateProtector("FitTrack.KnownDevice.v1").ToTimeLimitedDataProtector();

    private string CookieName => env.IsDevelopment() ? "FitTrack.Device" : "__Host-FitTrack-Device";

    public bool Matches(HttpRequest request, Guid userId)
    {
        if (!request.Cookies.TryGetValue(CookieName, out var value) || string.IsNullOrEmpty(value)) return false;
        try { return _protector.Unprotect(value) == userId.ToString("N"); }
        catch (System.Security.Cryptography.CryptographicException) { return false; }
    }

    public void Remember(HttpResponse response, Guid userId) =>
        response.Cookies.Append(CookieName, _protector.Protect(userId.ToString("N"), Lifetime), new CookieOptions
        {
            HttpOnly = true,
            Secure = !env.IsDevelopment(),
            SameSite = SameSiteMode.Strict,
            Path = "/",
            MaxAge = Lifetime,
        });
}
