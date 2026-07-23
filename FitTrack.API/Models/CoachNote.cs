namespace FitTrack.API.Models;

/// <summary>
/// Kalıcı koç hafızası — chat geçmişi 3 günde silinir, bunlar kalır.
/// Sakatlık, tercih, hedef bağlamı gibi uzun vadeli notlar.
/// </summary>
public class CoachNote
{
    public Guid Id { get; set; }
    public string Category { get; set; } = "fact"; // injury | preference | goal | fact
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
