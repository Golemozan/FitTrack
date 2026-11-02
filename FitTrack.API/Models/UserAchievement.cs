using System.Text.Json.Serialization;
namespace FitTrack.API.Models;

/// <summary>
/// Açılmış bir başarım kademesi ("protein-avcisi" 2. kademe). Başarımın kendisi veriden hesaplanır;
/// bu satır yalnız "ne zaman açıldı" ve "kullanıcı gördü mü" bilgisini kalıcı tutar. Kayıt sonradan
/// silinse bile açılmış başarım geri alınmaz.
/// </summary>
public class UserAchievement : IUserOwned
{
    [JsonIgnore]
    public Guid UserId { get; set; }

    public Guid Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public int Tier { get; set; }
    public DateTime UnlockedAt { get; set; }
    public DateTime? SeenAt { get; set; }
}

public record AchievementTierView(int Level, double Target, DateTime? UnlockedAt);

public record AchievementView(
    string Key,
    string Category,
    string Title,
    string Description,
    string Icon,
    string Unit,
    double Value,
    int Level,
    int MaxLevel,
    double? NextTarget,
    bool IsNew,
    string? Hint,
    List<AchievementTierView> Tiers);
