using System.Security.Cryptography;
using System.Text;

namespace FitTrack.API.Security;

/// <summary>
/// Kullanıcı API anahtarlarını AES-256-GCM ile şifreler.
///
/// Neden böyle:
/// - Ana anahtar <c>FITTRACK_ENCRYPTION_KEY</c> ortam değişkeninde, veritabanı volume'unda değil.
///   Veritabanı ya da yedeği sızarsa anahtarlar okunamaz.
/// - Kullanıcı kimliği "ek doğrulanmış veri" (AAD) olarak şifrelemeye bağlı. Veritabanına yazabilen
///   biri A'nın şifreli anahtarını B'nin satırına kopyalasa çözme başarısız olur.
/// - Biçim <c>v1:&lt;anahtar-kimliği&gt;:&lt;base64(nonce|tag|metin)&gt;</c>. Ana anahtar değiştirilirken eskisi
///   <c>FITTRACK_ENCRYPTION_KEY_PREVIOUS</c>'a konur; eski kayıtlar okunur ve ilk kullanımda yeni anahtarla yazılır.
///
/// Üretimde ana anahtar yoksa <see cref="IsAvailable"/> false olur ve anahtar kaydı/kullanımı tamamen kapanır.
/// </summary>
public sealed class KeyProtector
{
    private const string Version = "v1";
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly (string Id, byte[] Key)? _current;
    private readonly (string Id, byte[] Key)? _previous;

    public KeyProtector(IConfiguration cfg, IHostEnvironment env, ILogger<KeyProtector> log)
    {
        _current = Load(cfg["FITTRACK_ENCRYPTION_KEY"], "FITTRACK_ENCRYPTION_KEY", log);
        _previous = Load(cfg["FITTRACK_ENCRYPTION_KEY_PREVIOUS"], "FITTRACK_ENCRYPTION_KEY_PREVIOUS", log);

        if (_current is null && env.IsDevelopment())
        {
            _current = DevKey(log);
        }

        if (_current is null)
            log.LogError("FITTRACK_ENCRYPTION_KEY yok ya da geçersiz — AI anahtarı kaydetme ve kullanma kapalı. " +
                         "Üret: [Convert]::ToBase64String((1..32 | % {{ Get-Random -Max 256 }}))  (ya da openssl rand -base64 32)");
    }

    public bool IsAvailable => _current is not null;

    public string Protect(string plaintext, Guid userId)
    {
        var (id, key) = _current ?? throw new InvalidOperationException("Şifreleme anahtarı yok.");
        var data = Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var tag = new byte[TagSize];
        var cipher = new byte[data.Length];

        using (var aes = new AesGcm(key, TagSize))
            aes.Encrypt(nonce, data, cipher, tag, userId.ToByteArray());
        CryptographicOperations.ZeroMemory(data);

        var blob = new byte[NonceSize + TagSize + cipher.Length];
        nonce.CopyTo(blob, 0);
        tag.CopyTo(blob, NonceSize);
        cipher.CopyTo(blob, NonceSize + TagSize);
        return $"{Version}:{id}:{Convert.ToBase64String(blob)}";
    }

    /// <summary>
    /// Çözer. <paramref name="needsRewrap"/> true ise kayıt eski ana anahtarla yazılmış demektir;
    /// çağıran yeniden şifreleyip kaydetmeli.
    /// </summary>
    public bool TryUnprotect(string stored, Guid userId, out string plaintext, out bool needsRewrap)
    {
        plaintext = "";
        needsRewrap = false;

        var parts = stored.Split(':', 3);
        if (parts.Length != 3 || parts[0] != Version) return false;

        byte[] key;
        if (_current is { } cur && cur.Id == parts[1]) key = cur.Key;
        else if (_previous is { } prev && prev.Id == parts[1]) { key = prev.Key; needsRewrap = true; }
        else return false;

        byte[] blob;
        try { blob = Convert.FromBase64String(parts[2]); }
        catch (FormatException) { return false; }
        if (blob.Length < NonceSize + TagSize) return false;

        var nonce = blob.AsSpan(0, NonceSize);
        var tag = blob.AsSpan(NonceSize, TagSize);
        var cipher = blob.AsSpan(NonceSize + TagSize);
        var data = new byte[cipher.Length];
        try
        {
            using var aes = new AesGcm(key, TagSize);
            aes.Decrypt(nonce, cipher, tag, data, userId.ToByteArray());
            plaintext = Encoding.UTF8.GetString(data);
            return true;
        }
        catch (CryptographicException) { return false; }
        finally { CryptographicOperations.ZeroMemory(data); }
    }

    private static (string, byte[])? Load(string? base64, string name, ILogger log)
    {
        if (string.IsNullOrWhiteSpace(base64)) return null;
        byte[] key;
        try { key = Convert.FromBase64String(base64.Trim()); }
        catch (FormatException) { log.LogError("{Name} base64 değil.", name); return null; }
        if (key.Length != 32) { log.LogError("{Name} 32 bayt olmalı, {Len} bayt.", name, key.Length); return null; }
        return (KeyId(key), key);
    }

    /// <summary>Anahtarın kendisini değil, özetinin ilk 8 hex'ini açığa çıkarır.</summary>
    private static string KeyId(byte[] key) => Convert.ToHexString(SHA256.HashData(key))[..8].ToLowerInvariant();

    /// <summary>
    /// Yalnız Development: ana anahtar verilmediyse makineye özel bir tane üretip kullanıcı profilinde saklar.
    /// Böylece yerelde uygulama çalışır, ama bu dosya asla üretime taşınmaz.
    /// </summary>
    private static (string, byte[]) DevKey(ILogger log)
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FitTrack");
        var path = Path.Combine(dir, "dev-encryption.key");
        Directory.CreateDirectory(dir);
        if (!File.Exists(path))
            File.WriteAllText(path, Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        var key = Convert.FromBase64String(File.ReadAllText(path).Trim());
        log.LogWarning("Geliştirme şifreleme anahtarı kullanılıyor: {Path}", path);
        return (KeyId(key), key);
    }
}
