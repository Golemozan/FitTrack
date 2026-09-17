using System.Text.Json.Serialization;
namespace FitTrack.API.Models;

public class WeightLog : IUserOwned
{
    [JsonIgnore]
    public Guid UserId { get; set; }

    public Guid Id { get; set; }
    public double WeightKg { get; set; }
    public DateTime LoggedAt { get; set; }
    public string? Notes { get; set; }
}
