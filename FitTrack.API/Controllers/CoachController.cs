using System.Text.Json.Nodes;
using FitTrack.API.Data;
using FitTrack.API.Models;
using FitTrack.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FitTrack.API.Controllers;

/// <summary>İnce HTTP sarmalayıcı — koçun beyni <see cref="CoachService"/>'te.</summary>
[ApiController]
[Route("api/coach")]
public class CoachController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly CoachService _coach;

    public CoachController(AppDbContext db, CoachService coach)
    {
        _db = db;
        _coach = coach;
    }

    // GET /api/coach/history → last 3 days of conversation
    [HttpGet("history")]
    public async Task<ActionResult<List<object>>> History()
    {
        var since = DateTime.Now.Date.AddDays(-2);
        var messages = await _db.CoachMessages
            .Where(m => m.CreatedAt >= since)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new { m.Role, m.Content })
            .ToListAsync();
        return Ok(messages);
    }

    // POST /api/coach/chat
    [HttpPost("chat")]
    public async Task<ActionResult<CoachChatResponse>> Chat(CoachChatRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_coach.ApiKey))
            return StatusCode(500, new { error = "Anthropic API anahtarı ayarlı değil (Anthropic:ApiKey)." });

        if (req.Messages.Count == 0)
            return BadRequest(new { error = "Boş mesaj." });

        // Save the user's last message to history.
        var lastUser = req.Messages.LastOrDefault(m => m.Role == "user");
        if (lastUser is not null)
            _db.CoachMessages.Add(new CoachMessageRecord { Id = Guid.NewGuid(), Role = "user", Content = lastUser.Content, CreatedAt = DateTime.Now });

        var messages = new JsonArray();
        foreach (var m in req.Messages)
        {
            if (m.Role is not ("user" or "assistant") || string.IsNullOrWhiteSpace(m.Content)) continue;
            messages.Add(new JsonObject { ["role"] = m.Role, ["content"] = m.Content });
        }
        // Anthropic son turu assistant olan isteği reddediyor (prefill kaldırıldı).
        // İstemci düzenlenmiş mesajdan sonrasını kırpıyor; bu da eski istemcilere karşı ağ.
        while (messages.Count > 0 && (string?)messages[^1]?["role"] == "assistant")
            messages.RemoveAt(messages.Count - 1);

        if (messages.Count == 0) return BadRequest(new { error = "Geçerli mesaj yok." });

        var result = await _coach.ChatAsync(messages, ct: ct);
        if (!result.Ok) return StatusCode(502, new { error = "Koç yanıt veremedi." });

        if (!string.IsNullOrWhiteSpace(result.Reply))
            _db.CoachMessages.Add(new CoachMessageRecord { Id = Guid.NewGuid(), Role = "assistant", Content = result.Reply, CreatedAt = DateTime.Now });
        await _db.SaveChangesAsync(ct);

        return Ok(new CoachChatResponse { Reply = result.Reply, Actions = result.Actions });
    }
}
