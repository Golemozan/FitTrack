using System.Text.Json.Nodes;
using FitTrack.API.Data;
using FitTrack.API.Models;
using FitTrack.API.Security;
using FitTrack.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace FitTrack.API.Controllers;

/// <summary>İnce HTTP sarmalayıcı — koçun beyni <see cref="CoachService"/>'te.</summary>
[ApiController]
[Route("api/coach")]
public class CoachController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly CoachService _coach;

    private const int MaxMessages = 60;

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
    [EnableRateLimiting(RateLimits.Coach)]
    public async Task<ActionResult<CoachChatResponse>> Chat(CoachChatRequest req, CancellationToken ct)
    {
        if (!await _coach.HasKeyAsync(ct))
            return StatusCode(403, new { error = "AI özellikleri için kendi Anthropic API anahtarını eklemelisin.", code = "ai_key_missing" });

        if (req.Messages is null || req.Messages.Count == 0)
            return BadRequest(new { error = "Boş mesaj." });
        if (req.Messages.Count > MaxMessages || req.Messages.Any(m => (m.Content?.Length ?? 0) > CoachService.MaxMessageChars))
            return BadRequest(new { error = "Konuşma çok uzun. Yeni bir sohbet başlat." });

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
        if (!result.Ok)
            return result.KeyRejected
                ? StatusCode(403, new { error = "Anthropic anahtarını reddetti. Hesap ayarlarından yenile.", code = "ai_key_rejected" })
                : StatusCode(502, new { error = "Koç yanıt veremedi." });

        if (!string.IsNullOrWhiteSpace(result.Reply))
            _db.CoachMessages.Add(new CoachMessageRecord { Id = Guid.NewGuid(), Role = "assistant", Content = result.Reply, CreatedAt = DateTime.Now });
        await _db.SaveChangesAsync(ct);

        return Ok(new CoachChatResponse { Reply = result.Reply, Actions = result.Actions });
    }
}
