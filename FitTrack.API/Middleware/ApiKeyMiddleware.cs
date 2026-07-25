using System.Security.Cryptography;
using System.Text;

namespace FitTrack.API.Middleware;

/// <summary>
/// Kapı bekçisi. `/api/*` altındaki her istek geçerli bir <c>X-Api-Key</c> başlığı ister.
/// Anahtar sunucuda tanımlı değilse: yerelde kapı açık (geliştirme bozulmasın),
/// üretimde kapı tamamen kapalı — yanlışlıkla korumasız yayına çıkmak imkânsız.
/// </summary>
public class ApiKeyMiddleware
{
    public const string HeaderName = "X-Api-Key";

    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyMiddleware> _log;
    private readonly byte[]? _expected;
    private readonly bool _isDevelopment;

    public ApiKeyMiddleware(
        RequestDelegate next,
        IConfiguration cfg,
        IHostEnvironment env,
        ILogger<ApiKeyMiddleware> log)
    {
        _next = next;
        _log = log;
        _isDevelopment = env.IsDevelopment();

        // CoachService'teki anahtar okuma deseninin aynısı: önce config, sonra ortam değişkeni.
        var key = cfg["Auth:ApiKey"] ?? Environment.GetEnvironmentVariable("FITTRACK_API_KEY");
        _expected = string.IsNullOrWhiteSpace(key) ? null : Encoding.UTF8.GetBytes(key);

        if (_expected is null)
        {
            if (_isDevelopment)
                _log.LogWarning("FITTRACK_API_KEY yok — API yerel geliştirmede korumasız.");
            else
                _log.LogError("FITTRACK_API_KEY yok — API tamamen kapalı. Ortam değişkenini ayarla.");
        }
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        // Sadece API korunur. CORS ön kontrolü (preflight) özel başlık taşımaz, geçmeli.
        if (!ctx.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
            || HttpMethods.IsOptions(ctx.Request.Method))
        {
            await _next(ctx);
            return;
        }

        if (_expected is null)
        {
            if (_isDevelopment)
            {
                await _next(ctx);
                return;
            }

            await DenyAsync(ctx, StatusCodes.Status503ServiceUnavailable,
                "API anahtarı sunucuda tanımlı değil.");
            return;
        }

        var supplied = ctx.Request.Headers[HeaderName].ToString();
        if (string.IsNullOrEmpty(supplied) || !Matches(supplied))
        {
            await DenyAsync(ctx, StatusCodes.Status401Unauthorized,
                "Geçersiz veya eksik API anahtarı.");
            return;
        }

        await _next(ctx);
    }

    /// <summary>Sabit süreli karşılaştırma — cevap gecikmesinden anahtar sızdırmaz.</summary>
    private bool Matches(string supplied) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(supplied), _expected!);

    private static async Task DenyAsync(HttpContext ctx, int status, string message)
    {
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/json; charset=utf-8";
        await ctx.Response.WriteAsJsonAsync(new { error = message });
    }
}
