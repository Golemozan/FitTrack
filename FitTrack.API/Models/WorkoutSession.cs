using System.Text.Json.Serialization;
namespace FitTrack.API.Models;

public class WorkoutSession : IUserOwned
{
    [JsonIgnore]
    public Guid UserId { get; set; }

    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime LoggedAt { get; set; }
    public List<Exercise> Exercises { get; set; } = new();
}
