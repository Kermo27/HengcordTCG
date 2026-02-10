using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HengcordTCG.Shared.Data;
using HengcordTCG.Shared.Models;

using HengcordTCG.Shared.Services;

namespace HengcordTCG.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly UserService _userService;

    public UsersController(AppDbContext context, UserService userService)
    {
        _context = context;
        _userService = userService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<User>>> GetUsers()
    {
        return await _context.Users.ToListAsync();
    }

    [HttpGet("{discordId}")]
    public async Task<ActionResult<User>> GetUserByDiscordId(ulong discordId)
    {
        var user = await _context.Users
            .Include(u => u.UserCards)
            .ThenInclude(uc => uc.Card)
            .FirstOrDefaultAsync(u => u.DiscordId == discordId);

        if (user == null)
        {
            return NotFound();
        }

        return user;
    }

    [HttpGet("top-gold")]
    public async Task<ActionResult<IEnumerable<User>>> GetTopRichUsers([FromQuery] int limit = 10)
    {
        return await _context.Users
            .OrderByDescending(u => u.Gold)
            .Take(limit)
            .ToListAsync();
    }

    [HttpPost("{discordId}/sync")]
    public async Task<ActionResult<User>> SyncUser(ulong discordId, [FromQuery] string username)
    {
        var user = await _userService.GetOrCreateUserAsync(discordId, username);
        return Ok(user);
    }

    [HttpPost("{discordId}/daily")]
    public async Task<ActionResult> ClaimDaily(ulong discordId, [FromQuery] string username)
    {
        var result = await _userService.ClaimDailyAsync(discordId, username);
        return Ok(new { result.success, result.amount, result.timeRemaining });
    }

    [HttpPost("{discordId}/gold")]
    public async Task<ActionResult> AddGold(ulong discordId, [FromQuery] long amount)
    {
        await _userService.AddGoldAsync(discordId, amount);
        return Ok();
    }
}
