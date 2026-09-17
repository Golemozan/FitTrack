using System.Text.Json.Serialization;

namespace FitTrack.API.Models;

public class Exercise : IUserOwned
{
    [JsonIgnore]
    public Guid UserId { get; set; }

    public Guid Id { get; set; }
    public Guid WorkoutSessionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string MuscleGroup { get; set; } = string.Empty;

    [JsonIgnore]
    public WorkoutSession? WorkoutSession { get; set; }

    public List<ExerciseSet> Sets { get; set; } = new();
}
