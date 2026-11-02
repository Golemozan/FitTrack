namespace FitTrack.API.Models;

/// <summary>
/// Çıkış yapılmış oturum. Çerez imzalı ve kendi başına geçerli olduğu için çıkışta tarayıcıdan silmek
/// yetmez — kopyalanmış bir çerez çalışmaya devam ederdi. Kimliği burada durduğu sürece reddedilir;
/// çerezin en uzun ömrü geçince satır temizlenir.
/// </summary>
public class RevokedSession
{
    public string Sid { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public DateTime ExpiresAt { get; set; }
}
