using System.Text.Json;
using FitTrack.API.Data;
using FitTrack.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FitTrack.API.Controllers;

/// <summary>Ana sayfanın özelleştirilebilir kart düzeni. Kayıt yoksa varsayılan düzen döner.</summary>
[ApiController]
[Route("api/home-layout")]
public class HomeLayoutController : ControllerBase
{
    public static readonly HashSet<string> Types = new() { "mood", "habits", "tiles", "coach", "tip", "note" };
    public static readonly HashSet<string> TipTopics = new() { "microplastic", "nutrition", "sleep", "training" };
    private const int MaxWidgets = 16;
    private const int MaxNotes = 5;
    private const int MaxNoteLength = 280;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static List<HomeWidget> Default() => new()
    {
        new() { Id = "mood", Type = "mood" },
        new() { Id = "habits", Type = "habits" },
        new() { Id = "tiles", Type = "tiles" },
        new() { Id = "tip", Type = "tip" },
        new() { Id = "coach", Type = "coach" },
    };

    private readonly AppDbContext _db;
    public HomeLayoutController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<HomeLayoutDto>> Get(CancellationToken ct)
    {
        var row = await _db.HomeLayouts.AsNoTracking().FirstOrDefaultAsync(ct);
        var widgets = row is null ? Default() : JsonSerializer.Deserialize<List<HomeWidget>>(row.WidgetsJson, Json) ?? Default();
        return new HomeLayoutDto { Widgets = widgets };
    }

    [HttpPut]
    public async Task<ActionResult<HomeLayoutDto>> Put(HomeLayoutDto req, CancellationToken ct)
    {
        var widgets = req.Widgets ?? new();
        if (widgets.Count > MaxWidgets) return BadRequest(new { error = $"En fazla {MaxWidgets} kart eklenebilir." });

        var clean = new List<HomeWidget>();
        var ids = new HashSet<string>();
        foreach (var w in widgets)
        {
            var id = (w.Id ?? "").Trim();
            if (id.Length is < 1 or > 40 || !ids.Add(id)) return BadRequest(new { error = "Geçersiz kart kimliği." });
            if (!Types.Contains(w.Type)) return BadRequest(new { error = "Bilinmeyen kart türü." });
            // Not dışındaki kartlar ana sayfada bir kez bulunur.
            if (w.Type != "note" && clean.Any(c => c.Type == w.Type)) return BadRequest(new { error = "Bu kart zaten ekli." });

            var item = new HomeWidget { Id = id, Type = w.Type };
            if (w.Type == "note")
            {
                var text = (w.Text ?? "").Trim();
                if (text.Length > MaxNoteLength) return BadRequest(new { error = $"Not en fazla {MaxNoteLength} karakter olabilir." });
                item.Text = text;
            }
            if (w.Type == "tip" && w.Topics is { Count: > 0 })
            {
                if (w.Topics.Any(t => !TipTopics.Contains(t))) return BadRequest(new { error = "Bilinmeyen konu." });
                item.Topics = w.Topics.Distinct().ToList();
            }
            clean.Add(item);
        }
        if (clean.Count(c => c.Type == "note") > MaxNotes) return BadRequest(new { error = $"En fazla {MaxNotes} not kartı eklenebilir." });

        var row = await _db.HomeLayouts.FirstOrDefaultAsync(ct);
        if (row is null)
        {
            row = new HomeLayout { Id = Guid.NewGuid() };
            _db.HomeLayouts.Add(row);
        }
        row.WidgetsJson = JsonSerializer.Serialize(clean, Json);
        row.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync(ct);
        return new HomeLayoutDto { Widgets = clean };
    }
}
