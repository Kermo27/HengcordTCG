using HengcordTCG.Shared.Data;
using HengcordTCG.Shared.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HengcordTCG.Server.Controllers;

[ApiController]
[Route("me")]
[Authorize]
public class WebMeController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserService _userService;

    public WebMeController(AppDbContext db, UserService userService)
    {
        _db = db;
        _userService = userService;
    }

    [HttpGet("balance")]
    public async Task<IActionResult> GetBalance()
    {
        if (!TryGetDiscordId(out var discordId))
        {
            return Unauthorized();
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.DiscordId == discordId);
        if (user is null)
        {
            return Ok(new { balance = 0L });
        }

        return Ok(new { balance = user.Gold });
    }

    [HttpPost("daily")]
    public async Task<IActionResult> ClaimDaily()
    {
        if (!TryGetDiscordId(out var discordId))
        {
            return Unauthorized();
        }

        var username = User.Identity?.Name ?? $"User_{discordId}";
        var result = await _userService.ClaimDailyAsync(discordId, username);

        return Ok(new
        {
            success = result.success,
            amount = result.amount,
            timeRemaining = result.timeRemaining?.ToString(@"hh\:mm\:ss")
        });
    }

    [HttpGet("collection")]
    public async Task<IActionResult> GetCollection()
    {
        if (!TryGetDiscordId(out var discordId))
        {
            return Unauthorized();
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.DiscordId == discordId);
        if (user is null)
        {
            return Ok(Array.Empty<object>());
        }

        var cards = await _db.UserCards
            .Include(uc => uc.Card)
            .Where(uc => uc.UserId == user.Id)
            .ToListAsync();

        return Ok(cards);
    }

    private bool TryGetDiscordId(out ulong discordId)
    {
        discordId = 0;
        var discordIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return ulong.TryParse(discordIdStr, out discordId);
    }
}

