using HengcordTCG.Shared.Data;
using HengcordTCG.Shared.Models;
using HengcordTCG.Shared.Services;
using HengcordTCG.Server.Extensions;
using HengcordTCG.Server.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HengcordTCG.Server.Controllers;

[ApiController]
[Route("shop")]
public class WebShopController : ControllerBase
{
    private readonly ShopService _shopService;
    private readonly AppDbContext _db;

    public WebShopController(ShopService shopService, AppDbContext db)
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
    [Authorize(AuthenticationSchemes = $"{JwtBearerDefaults.AuthenticationScheme},{ApiKeyAuthenticationOptions.DefaultScheme}")]
    public async Task<IActionResult> BuyPack([FromQuery] string packName = "Base Set")
    {
        if (!TryGetDiscordId(out var discordId))
        {
            return Unauthorized();
        }

        var username = User.Identity?.Name ?? $"User_{discordId}";
        var result = await _shopService.BuyPackAsync(discordId, username, packName);

        return Ok(new
        {
            success = result.success,
            message = result.message,
            cards = result.cards
        });
    }

    private bool TryGetDiscordId(out ulong discordId) => 
        ControllerBaseExtensions.TryGetDiscordId(this, out discordId);
}

