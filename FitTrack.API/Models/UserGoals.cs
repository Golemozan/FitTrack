using System.Text.Json.Serialization;
namespace FitTrack.API.Models;

public class UserGoals : IUserOwned
{
    [JsonIgnore]
    public Guid UserId { get; set; }

    public Guid Id { get; set; }
    public double CalorieGoal { get; set; }
    public double ProteinGoal { get; set; }
    public double CarbGoal { get; set; }
    public double FatGoal { get; set; }
    public DateTime UpdatedAt { get; set; }
}
