using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HengcordTCG.Shared.Models;
using HengcordTCG.Shared.Services;

namespace HengcordTCG.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
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
    public async Task<ActionResult<IEnumerable<PackType>>> GetPacks()
    {
        return await _db.PackTypes.ToListAsync();
    }

    [HttpPost("buy-pack")]
    public async Task<ActionResult<List<Card>>> BuyPack([FromQuery] ulong discordId, [FromQuery] string username, [FromQuery] string packName = "Base Set")
    {
        var result = await _shopService.BuyPackAsync(discordId, username, packName);
        
        if (!result.success)
        {
            return BadRequest(result.message);
        }

        return Ok(result.cards);
    }
}
