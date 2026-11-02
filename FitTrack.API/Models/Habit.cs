using System.Text.Json.Serialization;
namespace FitTrack.API.Models;

/// <summary>
/// Kullanıcının günlük alışkanlığı: "Su — 8 bardak", "Kreatin — 1", "10 bin adım — 1".
/// Hedefi 1 olan tik kutusu gibi, 1'den büyük olan sayaç gibi davranır. Silinen alışkanlık
/// arşivlenir; geçmiş kayıtları haftalık özette sayılmaya devam eder.
/// </summary>
public class Habit : IUserOwned
{
    [JsonIgnore]
    public Guid UserId { get; set; }

    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>Arayüzdeki ikon anahtarı (water, pill, steps, stretch, sleep, book, apple, check).</summary>
    public string Icon { get; set; } = "check";
    public int Target { get; set; } = 1;
    /// <summary>Sayacın birimi: "bardak", "dk". Tik kutusunda boş.</summary>
    public string? Unit { get; set; }
    public int SortOrder { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Bir alışkanlığın bir gündeki sayısı. (Alışkanlık, gün) başına en fazla bir satır.</summary>
public class HabitLog : IUserOwned
{
    [JsonIgnore]
    public Guid UserId { get; set; }

    public Guid Id { get; set; }
    public Guid HabitId { get; set; }
    /// <summary>Takvim günü (saat 00:00, kullanıcının saati — Dockerfile TZ).</summary>
    public DateTime Day { get; set; }
    public int Count { get; set; }
}

public class HabitToday
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = "check";
    public int Target { get; set; }
    public string? Unit { get; set; }
    public int SortOrder { get; set; }
    public int Count { get; set; }
}

public class HabitRequest
{
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = "check";
    public int Target { get; set; } = 1;
    public string? Unit { get; set; }
}

public class HabitLogRequest
{
    /// <summary>yyyy-MM-dd; boşsa bugün.</summary>
    public DateTime? Date { get; set; }
    public int Count { get; set; }
}
