using FitTrack.API.Data;
using FitTrack.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FitTrack.API.Controllers;

[ApiController]
[Route("api/profile")]
public class ProfileController : ControllerBase
{
    private readonly AppDbContext _db;
    public ProfileController(AppDbContext db) => _db = db;

    // GET /api/profile  → single row, created lazily if missing
    [HttpGet]
    public async Task<ActionResult<Profile>> Get()
    {
        return await GetOrCreate();
    }

    // PUT /api/profile  → set height / target weight
    [HttpPut]
    public async Task<ActionResult<Profile>> Update(UpdateProfileRequest req)
    {
        var profile = await GetOrCreate();
        profile.HeightCm = req.HeightCm;
        profile.TargetWeightKg = req.TargetWeightKg;
        profile.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(profile);
    }

    private async Task<Profile> GetOrCreate()
    {
        var profile = await _db.Profiles.FirstOrDefaultAsync();
        if (profile is null)
        {
            profile = new Profile
            {
                Id = Guid.NewGuid(),
                UpdatedAt = DateTime.UtcNow,
            };
            _db.Profiles.Add(profile);
            await _db.SaveChangesAsync();
        }
        return profile;
    }
}
