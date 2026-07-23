using System.ComponentModel.DataAnnotations;

namespace FitTrack.API.Models;

/// <summary>Basit anahtar-değer deposu (Telegram chat id, son özet tarihi vb.).</summary>
public class AppSetting
{
    [Key]
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
