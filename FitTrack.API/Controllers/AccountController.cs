// EF1002: SQL'e yalnız SchemaUpgrade.OwnedTables sabit listesinden tablo adı giriyor; tüm değerler parametre.
#pragma warning disable EF1002
using System.Security.Cryptography;
using System.Text;
using FitTrack.API.Data;
using FitTrack.API.Models;
using FitTrack.API.Security;
using FitTrack.API.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace FitTrack.API.Controllers;

public record SaveApiKeyRequest(string Key);
public record ApiKeyStatus(bool HasKey, string? Hint, DateTime? CreatedAt, DateTime? LastUsedAt, bool StorageAvailable);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public record PasswordConfirmRequest(string Password);
public record LegacyClaimRequest(string LegacyPassword);
public record TelegramStatus(bool BotEnabled, bool Linked);
public record TelegramLinkCode(string Code, DateTime ExpiresAt);

[ApiController]
[Route("api/account")]
public class AccountController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly CurrentUser _current;
    private readonly IPasswordHasher<User> _hasher;
    private readonly ApiKeyService _keys;
    private readonly IConfiguration _cfg;
    private readonly ILogger<AccountController> _log;

    public AccountController(AppDbContext db, CurrentUser current, IPasswordHasher<User> hasher,
        ApiKeyService keys, IConfiguration cfg, ILogger<AccountController> log)
    {
        _db = db;
        _current = current;
        _hasher = hasher;
        _keys = keys;
        _cfg = cfg;
        _log = log;
    }

    // ===================== AI ANAHTARI =====================

    [HttpGet("ai-key")]
    public async Task<ActionResult<ApiKeyStatus>> GetKey(CancellationToken ct)
    {
        var record = await _keys.GetRecordAsync(_current.Require(), ct);
        return Ok(new ApiKeyStatus(record is not null && _keys.StorageAvailable, record?.Hint,
            record?.CreatedAt, record?.LastUsedAt, _keys.StorageAvailable));
    }

    [HttpPut("ai-key")]
    [EnableRateLimiting(RateLimits.Sensitive)]
    public async Task<ActionResult<ApiKeyStatus>> SaveKey(SaveApiKeyRequest req, CancellationToken ct)
    {
        if (!_keys.StorageAvailable)
            return StatusCode(503, new { error = "Anahtar saklama sunucuda yapılandırılmamış." });

        var key = (req.Key ?? "").Trim();
        if (!ApiKeyService.LooksLikeKey(key))
            return BadRequest(new { error = "Bu bir Anthropic API anahtarına benzemiyor." });

        switch (await _keys.ValidateAsync(key, ct))
        {
            case KeyCheck.Invalid:
                return BadRequest(new { error = "Anthropic bu anahtarı reddetti. Doğru kopyaladığından emin ol." });
            case KeyCheck.Unreachable:
                return StatusCode(502, new { error = "Anahtar şu an doğrulanamadı. Birazdan tekrar dene." });
        }

        var record = await _keys.SaveAsync(_current.Require(), key, ct);
        return Ok(new ApiKeyStatus(true, record.Hint, record.CreatedAt, record.LastUsedAt, true));
    }

    [HttpDelete("ai-key")]
    public async Task<IActionResult> DeleteKey(CancellationToken ct)
    {
        await _keys.DeleteAsync(_current.Require(), ct);
        return NoContent();
    }

    // ===================== OTURUM & PAROLA =====================

    [HttpPost("password")]
    [EnableRateLimiting(RateLimits.Sensitive)]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest req, CancellationToken ct)
    {
        var user = await LoadUserAsync(ct);
        if (user is null) return Unauthorized();

        if (!Verify(user, req.CurrentPassword))
            return BadRequest(new { error = "Mevcut parola hatalı." });

        var error = AuthController.ValidatePassword(req.NewPassword, user.Email);
        if (error is not null) return BadRequest(new { error });

        user.PasswordHash = _hasher.HashPassword(user, req.NewPassword);
        user.SessionVersion++; // diğer tüm cihazlar düşer
        await _db.SaveChangesAsync(ct);
        await AuthController.SignInAsync(HttpContext, user); // bu cihaz açık kalır
        return NoContent();
    }

    [HttpPost("logout-all")]
    public async Task<IActionResult> LogoutAll(CancellationToken ct)
    {
        var user = await LoadUserAsync(ct);
        if (user is null) return Unauthorized();
        user.SessionVersion++;
        await _db.SaveChangesAsync(ct);
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    /// <summary>Hesabı ve ona ait her satırı kalıcı olarak siler. Parola yeniden istenir.</summary>
    [HttpPost("delete")]
    [EnableRateLimiting(RateLimits.Sensitive)]
    public async Task<IActionResult> DeleteAccount(PasswordConfirmRequest req, CancellationToken ct)
    {
        var user = await LoadUserAsync(ct);
        if (user is null) return Unauthorized();
        if (!Verify(user, req.Password)) return BadRequest(new { error = "Parola hatalı." });

        var id = SchemaUpgrade.SqlGuid(user.Id);
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        foreach (var table in SchemaUpgrade.OwnedTables)
            await _db.Database.ExecuteSqlRawAsync($"DELETE FROM \"{table}\" WHERE \"UserId\" = {{0}}", new object[] { id }, ct);
        await _db.UserApiKeys.Where(k => k.UserId == user.Id).ExecuteDeleteAsync(ct);
        await _db.Users.Where(u => u.Id == user.Id).ExecuteDeleteAsync(ct);
        await tx.CommitAsync(ct);

        _log.LogInformation("Hesap silindi: {UserId}", user.Id);
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    // ===================== TELEGRAM =====================

    [HttpGet("telegram")]
    public async Task<ActionResult<TelegramStatus>> Telegram(CancellationToken ct)
    {
        var user = await LoadUserAsync(ct);
        if (user is null) return Unauthorized();
        return Ok(new TelegramStatus(TelegramBotService.IsEnabled(_cfg), user.TelegramChatId is not null));
    }

    /// <summary>
    /// 10 dakika geçerli, tek kullanımlık kod. Sunucuda yalnız özeti durur.
    /// Kullanıcı bota <c>/link KOD</c> yazınca sohbet bu hesaba bağlanır.
    /// </summary>
    [HttpPost("telegram/link-code")]
    [EnableRateLimiting(RateLimits.Sensitive)]
    public async Task<ActionResult<TelegramLinkCode>> CreateLinkCode(CancellationToken ct)
    {
        if (!TelegramBotService.IsEnabled(_cfg))
            return StatusCode(503, new { error = "Telegram botu bu sunucuda kapalı." });

        var user = await LoadUserAsync(ct);
        if (user is null) return Unauthorized();

        var code = TelegramBotService.NewLinkCode();
        user.TelegramLinkCodeHash = TelegramBotService.HashLinkCode(code);
        user.TelegramLinkCodeExpiresAt = DateTime.UtcNow.AddMinutes(10);
        await _db.SaveChangesAsync(ct);
        return Ok(new TelegramLinkCode(code, user.TelegramLinkCodeExpiresAt.Value));
    }

    [HttpDelete("telegram")]
    public async Task<IActionResult> UnlinkTelegram(CancellationToken ct)
    {
        var user = await LoadUserAsync(ct);
        if (user is null) return Unauthorized();
        user.TelegramChatId = null;
        user.TelegramLinkCodeHash = null;
        user.TelegramLinkCodeExpiresAt = null;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ===================== ESKİ VERİ =====================

    /// <summary>
    /// Tek kullanıcılı dönemden kalan veriler kimseye ait değil. Onları sahiplenmek için eski uygulama
    /// parolası (<c>FITTRACK_API_KEY</c>) gerekir — yani ancak eski sahibi alabilir, ilk kayıt olan değil.
    /// </summary>
    [HttpGet("legacy")]
    public ActionResult<object> Legacy() =>
        Ok(new { available = LegacySecret() is not null && SchemaUpgrade.HasLegacyData(_db) });

    [HttpPost("legacy/claim")]
    [EnableRateLimiting(RateLimits.Legacy)]
    public async Task<IActionResult> ClaimLegacy(LegacyClaimRequest req, CancellationToken ct)
    {
        var secret = LegacySecret();
        if (secret is null || !SchemaUpgrade.HasLegacyData(_db))
            return NotFound(new { error = "Sahiplenilecek eski veri yok." });

        var supplied = SHA256.HashData(Encoding.UTF8.GetBytes(req.LegacyPassword ?? ""));
        var expected = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
        if (!CryptographicOperations.FixedTimeEquals(supplied, expected))
            return BadRequest(new { error = "Eski parola hatalı." });

        var user = await LoadUserAsync(ct);
        if (user is null) return Unauthorized();

        var me = SchemaUpgrade.SqlGuid(user.Id);
        var legacy = SchemaUpgrade.SqlGuid(AppDbContext.LegacyOwner);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        // Tekil kayıtlar (hedef, profil): eski kayıt varsa hesabın yeni açtığı boş kaydın yerini alır.
        foreach (var single in new[] { "UserGoals", "Profiles" })
        {
            await _db.Database.ExecuteSqlRawAsync(
                $"DELETE FROM \"{single}\" WHERE \"UserId\" = {{0}} AND EXISTS (SELECT 1 FROM \"{single}\" WHERE \"UserId\" = {{1}})",
                new object[] { me, legacy }, ct);
        }
        foreach (var table in SchemaUpgrade.OwnedTables)
        {
            await _db.Database.ExecuteSqlRawAsync(
                $"UPDATE \"{table}\" SET \"UserId\" = {{0}} WHERE \"UserId\" = {{1}}",
                new object[] { me, legacy }, ct);
        }

        // Eski Telegram sahibini de bu hesaba taşı (başka hesaba bağlı değilse).
        var legacyChat = TelegramBotService.LegacyChatId(_cfg, _db);
        if (legacyChat is not null && user.TelegramChatId is null
            && !await _db.Users.AnyAsync(u => u.TelegramChatId == legacyChat, ct))
        {
            user.TelegramChatId = legacyChat;
            await _db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);

        _log.LogInformation("Eski veriler hesaba taşındı: {UserId}", user.Id);
        return NoContent();
    }

    // ===================== yardımcılar =====================

    private Task<User?> LoadUserAsync(CancellationToken ct)
    {
        var id = _current.Id;
        return id is null ? Task.FromResult<User?>(null) : _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
    }

    private bool Verify(User user, string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length > AuthController.MaxPassword) return false;
        return _hasher.VerifyHashedPassword(user, user.PasswordHash, password) != PasswordVerificationResult.Failed;
    }

    private string? LegacySecret()
    {
        var key = _cfg["Auth:ApiKey"] ?? _cfg["FITTRACK_API_KEY"];
        return string.IsNullOrWhiteSpace(key) ? null : key;
    }
}
