using System.Net.Mail;
using System.Security.Claims;
using FitTrack.API.Data;
using FitTrack.API.Models;
using FitTrack.API.Security;
using FitTrack.API.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace FitTrack.API.Controllers;

public record RegisterRequest(string Email, string Password, string DisplayName);
public record LoginRequest(string Email, string Password);

public record SessionResponse(Guid Id, string Email, string DisplayName, bool AiEnabled, bool AiKeyStorageAvailable);

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    public const int MinPassword = 10;
    public const int MaxPassword = 128;
    /// <summary>Aynı e-posta + aynı IP: bu kadar hatadan sonra o IP o hesap için 15 dk susturulur.</summary>
    private const int MaxFailedPerSource = 5;
    /// <summary>Hesabın toplam hatası (her IP'den): dağıtık denemeye karşı ikinci eşik.</summary>
    private const int MaxFailedPerAccount = 30;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private const string GenericLoginError = "E-posta ya da parola hatalı.";

    /// <summary>
    /// Olmayan hesaba giriş denemesi de gerçek bir PBKDF2 doğrulaması kadar sürsün —
    /// cevap süresinden "bu e-posta kayıtlı mı" okunamasın.
    /// </summary>
    private static readonly string DummyHash = new PasswordHasher<User>().HashPassword(new User(), Guid.NewGuid().ToString());

    private readonly AppDbContext _db;
    private readonly IPasswordHasher<User> _hasher;
    private readonly ApiKeyService _keys;
    private readonly ILogger<AuthController> _log;
    private readonly IMemoryCache _cache;
    private readonly RegistrationGate _gate;
    private readonly KnownDevice _device;

    public AuthController(AppDbContext db, IPasswordHasher<User> hasher, ApiKeyService keys, ILogger<AuthController> log,
        IMemoryCache cache, RegistrationGate gate, KnownDevice device)
    {
        _db = db;
        _hasher = hasher;
        _keys = keys;
        _log = log;
        _cache = cache;
        _gate = gate;
        _device = device;
    }

    [AllowAnonymous]
    [EnableRateLimiting(RateLimits.Auth)]
    [HttpPost("register")]
    public async Task<ActionResult<SessionResponse>> Register(RegisterRequest req, CancellationToken ct)
    {
        var email = NormalizeEmail(req.Email);
        if (email is null) return BadRequest(new { error = "Geçerli bir e-posta gir." });

        var name = (req.DisplayName ?? "").Trim();
        if (name.Length is < 1 or > 40) return BadRequest(new { error = "İsim 1-40 karakter olmalı." });

        var passwordError = ValidatePassword(req.Password, email);
        if (passwordError is not null) return BadRequest(new { error = passwordError });

        if (await _db.Users.AnyAsync(u => u.Email == email, ct))
            return Conflict(new { error = "Bu e-posta ile kayıtlı bir hesap var." });

        // IP sınırının yanında sunucu geneli tavan: IP taklit edilse bile sınırsız hesap açılamasın.
        if (!_gate.TryEnter())
            return StatusCode(StatusCodes.Status429TooManyRequests, new { error = "Şu an yeni kayıt alınamıyor. Biraz sonra tekrar dene." });

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            DisplayName = name,
            SessionVersion = 1,
            CreatedAt = DateTime.UtcNow,
        };
        user.PasswordHash = _hasher.HashPassword(user, req.Password);
        _db.Users.Add(user);

        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException) // aynı anda iki kayıt: benzersiz indeks yakalar
        {
            return Conflict(new { error = "Bu e-posta ile kayıtlı bir hesap var." });
        }

        _log.LogInformation("Yeni hesap: {UserId}", user.Id);
        await SignInAsync(user);
        return Ok(await ToSession(user, ct));
    }

    [AllowAnonymous]
    [EnableRateLimiting(RateLimits.Auth)]
    [HttpPost("login")]
    public async Task<ActionResult<SessionResponse>> Login(LoginRequest req, CancellationToken ct)
    {
        var email = NormalizeEmail(req.Email);
        var password = req.Password ?? "";
        var user = email is null ? null : await _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

        // Kilit hesabı değil kaynağı (e-posta + IP) susturur: saldırgan kendini kilitler, sahibi başka
        // yerden girebilir. Hesap geneli eşik dağıtık denemeye karşı. Kilitliyken de cevap ve süre
        // yanlış parolayla birebir aynı — kilit durumu "bu hesap var" diye okunamasın.
        var sourceKey = $"login-fail:{email}:{HttpContext.Connection.RemoteIpAddress}";
        var sourceLocked = _cache.TryGetValue(sourceKey, out int sourceFails) && sourceFails >= MaxFailedPerSource;
        // Hesap kilidi, bu hesaba daha önce girmiş cihazı durdurmaz (bkz. KnownDevice).
        var accountLocked = user?.LockoutUntil is { } until && until > DateTime.UtcNow && !_device.Matches(Request, user.Id);

        if (user is null || password.Length > MaxPassword || sourceLocked || accountLocked)
        {
            _hasher.VerifyHashedPassword(new User(), DummyHash, password.Length > MaxPassword ? "" : password);
            if (!sourceLocked) CountFailure(sourceKey, sourceFails);
            return Unauthorized(new { error = GenericLoginError });
        }

        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (result == PasswordVerificationResult.Failed)
        {
            CountFailure(sourceKey, sourceFails);
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= MaxFailedPerAccount)
            {
                user.LockoutUntil = DateTime.UtcNow.Add(LockoutDuration);
                user.FailedLoginCount = 0;
                _log.LogWarning("Hesap geçici olarak kilitlendi (dağıtık deneme): {UserId}", user.Id);
            }
            await _db.SaveChangesAsync(ct);
            return Unauthorized(new { error = GenericLoginError });
        }

        _cache.Remove(sourceKey);
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
            user.PasswordHash = _hasher.HashPassword(user, password);
        user.FailedLoginCount = 0;
        user.LockoutUntil = null;
        await _db.SaveChangesAsync(ct);

        await SignInAsync(user);
        return Ok(await ToSession(user, ct));
    }

    [HttpPost("logout")]
    [AllowAnonymous] // süresi dolmuş çerezle de çıkış yapılabilsin
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        // Çerezi tarayıcıdan silmek yetmez; kopyası varsa o da ölsün diye oturum kimliği iptal listesine.
        var userId = CurrentUser.FromPrincipal(User);
        var sid = User.FindFirst(SessionValidation.SessionClaim)?.Value;
        if (userId is not null && sid is not null && !await _db.RevokedSessions.AnyAsync(r => r.Sid == sid, ct))
        {
            _db.RevokedSessions.Add(new RevokedSession { Sid = sid, UserId = userId.Value, ExpiresAt = DateTime.UtcNow.Add(SessionLifetime) });
            await _db.SaveChangesAsync(ct);
        }
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    /// <summary>Çerezin en uzun ömrü (Program.cs ExpireTimeSpan) + pay. İptal kaydı bu kadar tutulur.</summary>
    public static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(15);

    private void CountFailure(string key, int current) =>
        _cache.Set(key, current + 1, LockoutDuration);

    [HttpGet("me")]
    public async Task<ActionResult<SessionResponse>> Me(CancellationToken ct)
    {
        var id = CurrentUser.FromPrincipal(User);
        var user = id is null ? null : await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return Unauthorized();
        return Ok(await ToSession(user, ct));
    }

    // ---------- yardımcılar (AccountController da kullanır) ----------

    internal async Task<SessionResponse> ToSession(User user, CancellationToken ct) =>
        new(user.Id, user.Email, user.DisplayName, await _keys.HasKeyAsync(user.Id, ct), _keys.StorageAvailable);

    internal Task SignInAsync(User user)
    {
        _device.Remember(Response, user.Id);
        return SignInAsync(HttpContext, user);
    }

    public static Task SignInAsync(HttpContext http, User user)
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(SessionValidation.VersionClaim, user.SessionVersion.ToString()),
            new Claim(SessionValidation.SessionClaim, Guid.NewGuid().ToString("N")),
        }, CookieAuthenticationDefaults.AuthenticationScheme);

        return http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true, AllowRefresh = true });
    }

    public static string? NormalizeEmail(string? raw)
    {
        var email = (raw ?? "").Trim().ToLowerInvariant();
        if (email.Length is < 3 or > 254) return null;
        try
        {
            var parsed = new MailAddress(email);
            return parsed.Address == email && email.Contains('.', StringComparison.Ordinal) ? email : null;
        }
        catch (FormatException) { return null; }
    }

    public static string? ValidatePassword(string? password, string email)
    {
        password ??= "";
        if (password.Length < MinPassword) return $"Parola en az {MinPassword} karakter olmalı.";
        if (password.Length > MaxPassword) return $"Parola en fazla {MaxPassword} karakter olabilir.";
        if (string.Equals(password.Trim(), email, StringComparison.OrdinalIgnoreCase)) return "Parola e-postanla aynı olamaz.";
        if (password.Distinct().Count() < 4) return "Parola çok tekdüze — farklı karakterler kullan.";
        return PasswordPolicy.Check(password, email);
    }
}
