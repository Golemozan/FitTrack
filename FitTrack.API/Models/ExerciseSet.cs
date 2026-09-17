using System.Text.Json.Serialization;

namespace FitTrack.API.Models;

public class ExerciseSet : IUserOwned
{
    [JsonIgnore]
    public Guid UserId { get; set; }

    public Guid Id { get; set; }
    public Guid ExerciseId { get; set; }
    public int SetNumber { get; set; }
    public double WeightKg { get; set; }
    public int Reps { get; set; }
    public bool IsCompleted { get; set; }

    [JsonIgnore]
    public Exercise? Exercise { get; set; }
}
