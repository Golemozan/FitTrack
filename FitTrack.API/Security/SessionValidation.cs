using FitTrack.API.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace FitTrack.API.Security;

/// <summary>
/// Çerez imzalı olsa da her istekte hesabın hâlâ var olduğu ve oturum sürümünün güncel olduğu
/// kontrol edilir. Parola değişince, "tüm cihazlardan çık" denince ya da hesap silinince eski
/// çerezler anında geçersizleşir — süresinin dolmasını beklemez.
/// </summary>
public static class SessionValidation
{
    public const string VersionClaim = "fittrack:sv";

    public static async Task ValidateAsync(CookieValidatePrincipalContext ctx)
    {
        var userId = CurrentUser.FromPrincipal(ctx.Principal);
        var versionClaim = ctx.Principal?.FindFirst(VersionClaim)?.Value;

        var db = ctx.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        var version = userId is null
            ? null
            : await db.Users.AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => (int?)u.SessionVersion)
                .FirstOrDefaultAsync(ctx.HttpContext.RequestAborted);

        if (version is null || versionClaim != version.Value.ToString())
        {
            ctx.RejectPrincipal();
            await ctx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }
}

public static class RateLimits
{
    public const string Auth = "auth";
    public const string Coach = "coach";
    public const string Sensitive = "sensitive";
    /// <summary>Eski parola tahmini: kullanıcı ya da IP başına değil, küresel sınır — çok hesap açmak işe yaramasın.</summary>
    public const string Legacy = "legacy";
}
