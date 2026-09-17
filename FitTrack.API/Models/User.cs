using System.ComponentModel.DataAnnotations;

namespace FitTrack.API.Models;

/// <summary>
/// Bir kullanıcıya ait her satır bunu uygular. <see cref="Data.AppDbContext"/> bu arayüz
/// üzerinden sorgu filtresi koyar ve kaydederken sahipliği zorlar — controller'ın
/// unutması başka birinin verisine erişim demek olmasın diye kural tek yerde.
/// </summary>
public interface IUserOwned
{
    Guid UserId { get; set; }
}

public class User
{
    [Key]
    public Guid Id { get; set; }

    /// <summary>Küçük harfe çekilmiş, kırpılmış e-posta. Benzersiz.</summary>
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>PBKDF2 (ASP.NET Core Identity PasswordHasher v3). Düz parola hiçbir yerde tutulmaz.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Oturum çerezine yazılır; artınca o ana kadarki tüm oturumlar geçersizleşir.</summary>
    public int SessionVersion { get; set; } = 1;

    public int FailedLoginCount { get; set; }
    public DateTime? LockoutUntil { get; set; }

    public long? TelegramChatId { get; set; }
    /// <summary>Telegram bağlama kodunun SHA-256 özeti — kodun kendisi saklanmaz.</summary>
    public string? TelegramLinkCodeHash { get; set; }
    public DateTime? TelegramLinkCodeExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Kullanıcının kendi Anthropic anahtarı. Ayrı tabloda durur ki kullanıcı nesnesi yanlışlıkla
/// serileştirilse bile şifreli metin bile dışarı çıkmasın. Anahtar AES-256-GCM ile şifreli;
/// ana anahtar veritabanında değil ortam değişkeninde (bkz. <see cref="Security.KeyProtector"/>).
/// </summary>
public class UserApiKey
{
    [Key]
    public Guid UserId { get; set; }
    public string Ciphertext { get; set; } = string.Empty;
    /// <summary>Arayüzde tanımak için: "sk-ant-…abcd". Anahtarın geri kalanı hiç gösterilmez.</summary>
    public string Hint { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
}
