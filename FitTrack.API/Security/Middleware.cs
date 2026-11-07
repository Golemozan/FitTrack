using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Primitives;

namespace FitTrack.API.Security;

/// <summary>
/// Üretimde istemci adresi Railway'in belgelediği <c>X-Real-IP</c> başlığından alınır. Başlık yoksa ya da
/// tek bir geçerli IP değilse bağlantı adresi kalır. Yalnız üretimde eklenir (bkz. Program.cs).
/// </summary>
public class RealIpMiddleware
{
    public const string HeaderName = "X-Real-IP";

    private readonly RequestDelegate _next;
    public RealIpMiddleware(RequestDelegate next) => _next = next;

    public Task InvokeAsync(HttpContext ctx)
    {
        if (TryParse(ctx.Request.Headers[HeaderName]) is { } ip)
            ctx.Connection.RemoteIpAddress = ip;
        return _next(ctx);
    }

    /// <summary><see cref="IPAddress.TryParse(string, out IPAddress)"/> "1", "010.0.0.1", "[::1]:80" gibi biçimleri de kabul eder; onlar geçersiz sayılır.</summary>
    internal static IPAddress? TryParse(StringValues values)
    {
        if (values.Count != 1) return null;
        var raw = values[0]?.Trim();
        if (string.IsNullOrEmpty(raw) || !IPAddress.TryParse(raw, out var ip)) return null;
        if (ip.AddressFamily == AddressFamily.InterNetwork)
            return ip.ToString() == raw ? ip : null;
        if (raw.Contains('[') || raw.Contains('%') || ip.ScopeId != 0) return null;
        return ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip;
    }
}

/// <summary>
/// CSRF'ye ikinci kalkan (ilki SameSite=Strict çerez). Durum değiştiren her /api isteği
/// <c>X-Requested-With: FitTrack</c> başlığı taşımak zorunda. Başka bir site bu başlığı
/// CORS ön kontrolü olmadan ekleyemez, sunucu da CORS'a izin vermiyor.
/// </summary>
public class CsrfHeaderMiddleware
{
    public const string HeaderName = "X-Requested-With";
    public const string HeaderValue = "FitTrack";

    private readonly RequestDelegate _next;
    public CsrfHeaderMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext ctx)
    {
        var method = ctx.Request.Method;
        var unsafeMethod = !(HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method));

        if (unsafeMethod
            && ctx.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(ctx.Request.Headers[HeaderName], HeaderValue, StringComparison.Ordinal))
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            ctx.Response.ContentType = "application/json; charset=utf-8";
            await ctx.Response.WriteAsJsonAsync(new { error = "İstek reddedildi." });
            return;
        }

        await _next(ctx);
    }
}

/// <summary>Tarayıcı tarafı sertleştirme başlıkları. Uygulama dış kaynak yüklemiyor, CSP buna göre dar.</summary>
public class SecurityHeadersMiddleware
{
    private const string Csp =
        "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; " +
        "font-src 'self' data:; connect-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";

    private readonly RequestDelegate _next;
    private readonly bool _isDevelopment;

    public SecurityHeadersMiddleware(RequestDelegate next, IHostEnvironment env)
    {
        _next = next;
        _isDevelopment = env.IsDevelopment();
    }

    public Task InvokeAsync(HttpContext ctx)
    {
        ctx.Response.OnStarting(() =>
        {
            var h = ctx.Response.Headers;
            h["X-Content-Type-Options"] = "nosniff";
            h["X-Frame-Options"] = "DENY";
            h["Referrer-Policy"] = "no-referrer";
            h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
            h["Cross-Origin-Opener-Policy"] = "same-origin";
            h["Content-Security-Policy"] = Csp;
            if (!_isDevelopment)
                h["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
            if (ctx.Request.Path.StartsWithSegments("/api"))
                h["Cache-Control"] = "no-store"; // kişisel veri ara belleklerde kalmasın
            return Task.CompletedTask;
        });
        return _next(ctx);
    }
}
