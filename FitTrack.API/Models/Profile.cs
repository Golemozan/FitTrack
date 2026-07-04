namespace FitTrack.API.Models;

// Single-row personal constants the coach needs for context (and BMI).
public class Profile
{
    public Guid Id { get; set; }
    public double? HeightCm { get; set; }
    public double? TargetWeightKg { get; set; }
    public DateTime UpdatedAt { get; set; }
}
