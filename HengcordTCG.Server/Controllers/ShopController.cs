using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HengcordTCG.Shared.Models;
using HengcordTCG.Shared.Services;
using HengcordTCG.Server.Extensions;

namespace HengcordTCG.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class ShopController : ControllerBase
{
    private readonly ShopService _shopService;
    private readonly Shared.Data.AppDbContext _db;

    public ShopController(ShopService shopService, Shared.Data.AppDbContext db)
    {
        _shopService = shopService;
        _db = db;
    }

    [HttpGet("packs")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<PackType>>> GetPacks()
    {
        return await _db.PackTypes.ToListAsync();
    }

    [HttpPost("buy-pack")]
    public async Task<ActionResult<List<Card>>> BuyPack([FromQuery] ulong discordId, [FromQuery] string username, [FromQuery] string packName = "Base Set")
    {
        try
        {
            ValidationExtensions.ValidateDiscordId(discordId);
            ValidationExtensions.ValidateUsername(username);
            ValidationExtensions.ValidatePackName(packName);
            
            var result = await _shopService.BuyPackAsync(discordId, username, packName);
            
            if (!result.success)
            {
                return BadRequest(new { message = result.message });
            }

            return Ok(result.cards);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
