namespace FitTrack.API.Models;

public class UserGoals
{
    public Guid Id { get; set; }
    public double CalorieGoal { get; set; }
    public double ProteinGoal { get; set; }
    public double CarbGoal { get; set; }
    public double FatGoal { get; set; }
    public DateTime UpdatedAt { get; set; }
}
