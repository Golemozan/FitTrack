using System.Text.Json.Serialization;
namespace FitTrack.API.Models;

/// <summary>
/// Kaydedilmiş antrenman şablonu ("Push günü"). Yalnız iskeleti tutar — hareket, set sayısı,
/// hedef tekrar. Ağırlık şablonda yazılı değil: uygulanırken her hareketin son yapıldığı
/// değerlerden doldurulur, böylece şablon hiç eskimez.
/// </summary>
public class WorkoutTemplate : IUserOwned
{
    [JsonIgnore]
    public Guid UserId { get; set; }

    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public List<TemplateExercise> Exercises { get; set; } = new();
}

public class TemplateExercise : IUserOwned
{
    [JsonIgnore]
    public Guid UserId { get; set; }

    public Guid Id { get; set; }
    public Guid WorkoutTemplateId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string MuscleGroup { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public int SetCount { get; set; }
    public int TargetReps { get; set; }

    [JsonIgnore]
    public WorkoutTemplate? WorkoutTemplate { get; set; }
}

public class SaveTemplateRequest
{
    public string Name { get; set; } = string.Empty;
    /// <summary>Bu seansın hareketleri şablona kopyalanır.</summary>
    public Guid SessionId { get; set; }
}
