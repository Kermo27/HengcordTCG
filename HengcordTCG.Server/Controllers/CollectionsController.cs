using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HengcordTCG.Shared.Data;
using HengcordTCG.Shared.Models;

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
        var user = await _context.Users.FirstOrDefaultAsync(u => u.DiscordId == discordId);
        if (user == null) return NotFound("User not found.");

        return await _context.UserCards
            .Include(uc => uc.Card)
            .Where(uc => uc.UserId == user.Id)
            .ToListAsync();
    }
}
