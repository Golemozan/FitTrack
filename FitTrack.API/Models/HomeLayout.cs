using System.Text.Json.Serialization;
namespace FitTrack.API.Models;

/// <summary>
/// Kullanıcının ana sayfa düzeni: seri ve beslenme kartlarının altındaki özelleştirilebilir kartlar,
/// sırasıyla. Sunucuda durur ki telefon ve bilgisayar aynı ana sayfayı göstersin. Kullanıcı başına tek satır.
/// </summary>
public class HomeLayout : IUserOwned
{
    [JsonIgnore]
    public Guid UserId { get; set; }

    public Guid Id { get; set; }
    /// <summary>Doğrulanmış <see cref="HomeWidget"/> listesinin JSON hali.</summary>
    public string WidgetsJson { get; set; } = "[]";
    public DateTime UpdatedAt { get; set; }
}

public class HomeWidget
{
    /// <summary>İstemcinin ürettiği kararlı kimlik (sürükle-bırak anahtarı).</summary>
    public string Id { get; set; } = string.Empty;
    /// <summary>mood · habits · tiles · coach · tip · note</summary>
    public string Type { get; set; } = string.Empty;
    /// <summary>Yalnız "note": kullanıcının kendi notu.</summary>
    public string? Text { get; set; }
    /// <summary>Yalnız "tip": gösterilecek konular (boş = hepsi).</summary>
    public List<string>? Topics { get; set; }
}

public class HomeLayoutDto
{
    public List<HomeWidget> Widgets { get; set; } = new();
}
