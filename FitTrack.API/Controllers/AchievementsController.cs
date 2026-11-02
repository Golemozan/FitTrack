using FitTrack.API.Models;
using FitTrack.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace FitTrack.API.Controllers;

/// <summary>Başarımlar. Her okuma veriden yeniden hesaplar; yeni geçilen kademeler o an açılır.</summary>
[ApiController]
[Route("api/achievements")]
public class AchievementsController : ControllerBase
{
    private readonly AchievementService _service;
    public AchievementsController(AchievementService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<List<AchievementView>>> List(CancellationToken ct) => await _service.EvaluateAsync(ct);

    /// <summary>Kutlama gösterildi: "yeni" işaretleri kalkar, bir daha kutlanmaz.</summary>
    [HttpPost("seen")]
    public async Task<IActionResult> Seen(CancellationToken ct)
    {
        await _service.MarkSeenAsync(ct);
        return NoContent();
    }
}
