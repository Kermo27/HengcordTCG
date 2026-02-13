using HengcordTCG.Shared.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HengcordTCG.Server.Controllers;

[ApiController]
[Route("admin")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
public class WebAdminController : ControllerBase
{
    private readonly AppDbContext _db;

    public WebAdminController(AppDbContext db)
    {
        _db = db;
    }

    [HttpPost("give-gold")]
    public async Task<IActionResult> GiveGold([FromQuery] ulong discordId, [FromQuery] int amount)
    {
        if (!IsAdmin())
        {
            return Forbid();
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.DiscordId == discordId);
        if (user is null)
        {
            return NotFound(new { message = "User not found" });
        }

        user.Gold += amount;
        await _db.SaveChangesAsync();

        return Ok(new { balance = user.Gold });
    }

    [HttpPost("toggle-pack")]
    public async Task<IActionResult> TogglePack([FromQuery] string packName)
    {
        if (!IsAdmin())
        {
            return Forbid();
        }

        var pack = await _db.PackTypes.FirstOrDefaultAsync(p => p.Name == packName);
        if (pack is null)
        {
            return NotFound(new { message = "Pack not found" });
        }

        pack.IsAvailable = !pack.IsAvailable;
        await _db.SaveChangesAsync();

        return Ok(new { packName, isAvailable = pack.IsAvailable });
    }

    private bool IsAdmin() => User.HasClaim("is_admin", "true");
}

