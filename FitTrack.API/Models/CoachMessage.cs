using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;

namespace FitTrack.API.Models;

/// <summary>
/// Persisted coach conversation entry — separate from the DTO <see cref="Dtos.CoachMessage"/>.
/// </summary>
public class CoachMessageRecord : IUserOwned
{
    [JsonIgnore]
    public Guid UserId { get; set; }

    [Key]
    public Guid Id { get; set; }
    public string Role { get; set; } = "user"; // "user" or "assistant"
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
