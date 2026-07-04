namespace FitTrack.API.Models;

// A moment-in-time self-report the coach reads to track how Ozan feels vs. his
// diet/training. Scales are 1–5. Context tags where it happened (general /
// pre-workout / post-workout) so the coach can correlate mood with training.
public class CheckIn
{
    public Guid Id { get; set; }
    public int Mood { get; set; }     // 1 = kötü, 5 = harika
    public int Energy { get; set; }   // 1 = bitkin, 5 = enerjik
    public int Hunger { get; set; }   // 1 = çok aç, 5 = çok tok
    public string? Note { get; set; }
    public string? Context { get; set; } // "general" | "pre-workout" | "post-workout"
    public DateTime LoggedAt { get; set; }
}
