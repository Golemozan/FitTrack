namespace FitTrack.API.Models;

public class WorkoutSession
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime LoggedAt { get; set; }
    public List<Exercise> Exercises { get; set; } = new();
}
