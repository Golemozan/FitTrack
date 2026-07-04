namespace FitTrack.API.Models;

public class WeightLog
{
    public Guid Id { get; set; }
    public double WeightKg { get; set; }
    public DateTime LoggedAt { get; set; }
    public string? Notes { get; set; }
}
