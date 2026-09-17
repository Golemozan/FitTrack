using FitTrack.API.Data;
using FitTrack.API.Models;
using FitTrack.API.Security;
using Microsoft.EntityFrameworkCore;

namespace FitTrack.API.Services;

public enum KeyCheck { Valid, Invalid, Unreachable }

/// <summary>
/// Kullanıcının Anthropic anahtarının tek kapısı. Düz anahtar yalnız iki yerde bellekte bulunur:
/// kaydederken (doğrulama + şifreleme) ve Anthropic çağrısı yapılırken. Hiçbir uç onu geri döndürmez,
/// hiçbir log satırı onu yazmaz.
/// </summary>
public class ApiKeyService
{
    private const string ModelsUrl = "https://api.anthropic.com/v1/models?limit=1";

    private readonly AppDbContext _db;
    private readonly KeyProtector _protector;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<ApiKeyService> _log;

    public ApiKeyService(AppDbContext db, KeyProtector protector, IHttpClientFactory httpFactory, ILogger<ApiKeyService> log)
    {
        _db = db;
        _protector = protector;
        _httpFactory = httpFactory;
        _log = log;
    }

    public bool StorageAvailable => _protector.IsAvailable;

    public Task<UserApiKey?> GetRecordAsync(Guid userId, CancellationToken ct = default) =>
        _db.UserApiKeys.AsNoTracking().FirstOrDefaultAsync(k => k.UserId == userId, ct);

    public async Task<bool> HasKeyAsync(Guid userId, CancellationToken ct = default) =>
        _protector.IsAvailable && await _db.UserApiKeys.AnyAsync(k => k.UserId == userId, ct);

    /// <summary>Biçim kontrolü: boşluk yok, makul uzunluk, yazdırılabilir ASCII. Anthropic'e gitmeden eler.</summary>
    public static bool LooksLikeKey(string key) =>
        key.Length is >= 20 and <= 256 && key.All(c => c is > ' ' and < (char)127);

    /// <summary>
    /// Anthropic'in token harcamayan model listesi ucuyla anahtarı sınar.
    /// 401/403 → geçersiz. Ağ hatası ya da 5xx → belirsiz; bu durumda anahtar kaydedilmez.
    /// </summary>
    public async Task<KeyCheck> ValidateAsync(string key, CancellationToken ct)
    {
        var http = _httpFactory.CreateClient();
        http.Timeout = TimeSpan.FromSeconds(15);
        using var req = new HttpRequestMessage(HttpMethod.Get, ModelsUrl);
        req.Headers.Add("x-api-key", key);
        req.Headers.Add("anthropic-version", "2023-06-01");
        try
        {
            using var resp = await http.SendAsync(req, ct);
            var status = (int)resp.StatusCode;
            if (resp.IsSuccessStatusCode) return KeyCheck.Valid;
            if (status is 400 or 401 or 403) return KeyCheck.Invalid;
            _log.LogWarning("Anahtar doğrulaması belirsiz: Anthropic {Status}", status);
            return KeyCheck.Unreachable;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            // Exception mesajı anahtarı içermez; yine de yalnız tipini yazıyoruz.
            _log.LogWarning("Anahtar doğrulaması yapılamadı: {Type}", ex.GetType().Name);
            return KeyCheck.Unreachable;
        }
    }

    public async Task<UserApiKey> SaveAsync(Guid userId, string key, CancellationToken ct)
    {
        var record = await _db.UserApiKeys.FirstOrDefaultAsync(k => k.UserId == userId, ct);
        if (record is null)
        {
            record = new UserApiKey { UserId = userId };
            _db.UserApiKeys.Add(record);
        }
        record.Ciphertext = _protector.Protect(key, userId);
        record.Hint = MakeHint(key);
        record.CreatedAt = DateTime.UtcNow;
        record.LastUsedAt = null;
        await _db.SaveChangesAsync(ct);
        return record;
    }

    public async Task<bool> DeleteAsync(Guid userId, CancellationToken ct)
    {
        var deleted = await _db.UserApiKeys.Where(k => k.UserId == userId).ExecuteDeleteAsync(ct);
        return deleted > 0;
    }

    /// <summary>
    /// Çağrı anında çözülmüş anahtar. Kayıt çözülemiyorsa (ana anahtar değişmiş, veri bozulmuş,
    /// başka kullanıcının satırı kopyalanmış) null döner ve kullanıcıdan yeniden girmesi beklenir.
    /// </summary>
    public async Task<string?> GetPlaintextAsync(Guid userId, CancellationToken ct)
    {
        if (!_protector.IsAvailable) return null;
        var record = await _db.UserApiKeys.FirstOrDefaultAsync(k => k.UserId == userId, ct);
        if (record is null) return null;

        if (!_protector.TryUnprotect(record.Ciphertext, userId, out var key, out var needsRewrap))
        {
            _log.LogError("Kullanıcı {UserId} için API anahtarı çözülemedi.", userId);
            return null;
        }

        if (needsRewrap) record.Ciphertext = _protector.Protect(key, userId);
        record.LastUsedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return key;
    }

    private static string MakeHint(string key)
    {
        var tail = key[^4..];
        var head = key.StartsWith("sk-ant-", StringComparison.Ordinal) ? "sk-ant-" : "";
        return $"{head}…{tail}";
    }
}
