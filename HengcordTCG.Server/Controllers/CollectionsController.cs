using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HengcordTCG.Shared.Data;
using HengcordTCG.Shared.Models;
using HengcordTCG.Server.Extensions;

namespace HengcordTCG.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CollectionsController : ControllerBase
{
    private readonly AppDbContext _context;

    public CollectionsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("{discordId}")]
    public async Task<ActionResult<IEnumerable<UserCard>>> GetCollection(ulong discordId)
    {
        try
        {
            ValidationExtensions.ValidateDiscordId(discordId);
            
            var user = await _context.Users.FirstOrDefaultAsync(u => u.DiscordId == discordId);
            if (user == null) return NotFound(new { message = "User not found" });

            return await _context.UserCards
                .Include(uc => uc.Card)
                .Where(uc => uc.UserId == user.Id)
                .ToListAsync();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
