using System.Net.Mail;
using System.Security.Claims;
using FitTrack.API.Data;
using FitTrack.API.Models;
using FitTrack.API.Security;
using FitTrack.API.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
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
    private const int MaxFailedLogins = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Olmayan hesaba giriş denemesi de gerçek bir PBKDF2 doğrulaması kadar sürsün —
    /// cevap süresinden "bu e-posta kayıtlı mı" okunamasın.
    /// </summary>
    private static readonly string DummyHash = new PasswordHasher<User>().HashPassword(new User(), Guid.NewGuid().ToString());

    private readonly AppDbContext _db;
    private readonly IPasswordHasher<User> _hasher;
    private readonly ApiKeyService _keys;
    private readonly ILogger<AuthController> _log;

    public AuthController(AppDbContext db, IPasswordHasher<User> hasher, ApiKeyService keys, ILogger<AuthController> log)
    {
        _db = db;
        _hasher = hasher;
        _keys = keys;
        _log = log;
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
        const string generic = "E-posta ya da parola hatalı.";
        var email = NormalizeEmail(req.Email);
        var password = req.Password ?? "";
        var user = email is null ? null : await _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

        if (user is null || password.Length > MaxPassword)
        {
            _hasher.VerifyHashedPassword(new User(), DummyHash, password.Length > MaxPassword ? "" : password);
            return Unauthorized(new { error = generic });
        }

        if (user.LockoutUntil is { } until && until > DateTime.UtcNow)
        {
            var minutes = Math.Max(1, (int)Math.Ceiling((until - DateTime.UtcNow).TotalMinutes));
            return StatusCode(StatusCodes.Status429TooManyRequests,
                new { error = $"Çok fazla hatalı deneme. {minutes} dakika sonra tekrar dene." });
        }

        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (result == PasswordVerificationResult.Failed)
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= MaxFailedLogins)
            {
                user.LockoutUntil = DateTime.UtcNow.Add(LockoutDuration);
                user.FailedLoginCount = 0;
                _log.LogWarning("Hesap geçici olarak kilitlendi: {UserId}", user.Id);
            }
            await _db.SaveChangesAsync(ct);
            return Unauthorized(new { error = generic });
        }

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
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

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

    internal Task SignInAsync(User user) => SignInAsync(HttpContext, user);

    public static Task SignInAsync(HttpContext http, User user)
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(SessionValidation.VersionClaim, user.SessionVersion.ToString()),
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
        return null;
    }
}
