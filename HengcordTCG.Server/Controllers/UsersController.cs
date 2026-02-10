using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HengcordTCG.Shared.Data;
using HengcordTCG.Shared.Models;
using HengcordTCG.Shared.Services;
using HengcordTCG.Server.Extensions;

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
        try
        {
            ValidationExtensions.ValidateDiscordId(discordId);
            
            var user = await _context.Users
                .Include(u => u.UserCards)
                .ThenInclude(uc => uc.Card)
                .FirstOrDefaultAsync(u => u.DiscordId == discordId);

            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }

            return user;
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("top-gold")]
    public async Task<ActionResult<IEnumerable<User>>> GetTopRichUsers([FromQuery] int limit = 10)
    {
        try
        {
            ValidationExtensions.ValidateLimitParameter(limit);
            
            return await _context.Users
                .OrderByDescending(u => u.Gold)
                .Take(limit)
                .ToListAsync();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{discordId}/sync")]
    public async Task<ActionResult<User>> SyncUser(ulong discordId, [FromQuery] string username)
    {
        try
        {
            ValidationExtensions.ValidateDiscordId(discordId);
            ValidationExtensions.ValidateUsername(username);
            
            var user = await _userService.GetOrCreateUserAsync(discordId, username);
            return Ok(user);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{discordId}/daily")]
    public async Task<ActionResult> ClaimDaily(ulong discordId, [FromQuery] string username)
    {
        try
        {
            ValidationExtensions.ValidateDiscordId(discordId);
            ValidationExtensions.ValidateUsername(username);
            
            var result = await _userService.ClaimDailyAsync(discordId, username);
            return Ok(new { result.success, result.amount, result.timeRemaining });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{discordId}/gold")]
    public async Task<ActionResult> AddGold(ulong discordId, [FromQuery] long amount)
    {
        try
        {
            ValidationExtensions.ValidateDiscordId(discordId);
            ValidationExtensions.ValidateAmount(amount);
            
            await _userService.AddGoldAsync(discordId, amount);
            return Ok(new { message = $"Added {amount} gold to user {discordId}" });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
