using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HengcordTCG.Shared.Data;
using HengcordTCG.Shared.Models;
using HengcordTCG.Server.Extensions;
using HengcordTCG.Server.Services;

namespace HengcordTCG.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ICardService _cardService;
    private readonly IPackService _packService;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        AppDbContext context,
        ICardService cardService,
        IPackService packService,
        ILogger<AdminController> logger)
    {
        _context = context;
        _cardService = cardService;
        _packService = packService;
        _logger = logger;
    }

    [HttpPost("add-card")]
    public async Task<ActionResult> AddCard(Card card)
    {
        var result = await _cardService.AddAsync(card);
        if (result.IsFailure)
        {
            return BadRequest(result.Error.Message);
        }
        return Ok(result.Value);
    }

    [HttpPut("update-card/{id}")]
    public async Task<ActionResult> UpdateCard(int id, [FromBody] Card card)
    {
        var result = await _cardService.UpdateAsync(id, card);
        if (result.IsFailure)
        {
            return result.Error.Code == "NOT_FOUND" ? NotFound(result.Error.Message) : BadRequest(result.Error.Message);
        }
        return Ok(result.Value);
    }

    [HttpDelete("remove-card/{name}")]
    public async Task<ActionResult> RemoveCard(string name)
    {
        var result = await _cardService.DeleteAsync(name);
        if (result.IsFailure)
        {
            return result.Error.Code == "NOT_FOUND" ? NotFound(result.Error.Message) : BadRequest(result.Error.Message);
        }
        return Ok();
    }

    [HttpPost("give-gold")]
    public async Task<ActionResult> GiveGold([FromQuery] ulong discordId, [FromQuery] int amount)
    {
        try
        {
            ValidationExtensions.ValidateDiscordId(discordId);
            ValidationExtensions.ValidateAmount(amount);
            
            var user = await _context.Users.FirstOrDefaultAsync(u => u.DiscordId == discordId);
            if (user == null) return NotFound(new { message = "User not found" });
            
            user.Gold += amount;
            await _context.SaveChangesAsync();
            _logger.LogInformation("Gave {Amount} gold to user {DiscordId}", amount, discordId);
            return Ok(new { newBalance = user.Gold });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("set-gold")]
    public async Task<ActionResult> SetGold([FromQuery] ulong discordId, [FromQuery] int amount)
    {
        try
        {
            ValidationExtensions.ValidateDiscordId(discordId);
            if (amount < 0)
                throw new ArgumentException("Amount cannot be negative", nameof(amount));

            var user = await _context.Users.FirstOrDefaultAsync(u => u.DiscordId == discordId);
            if (user == null) return NotFound(new { message = "User not found" });
        
            user.Gold = amount;
            await _context.SaveChangesAsync();
            _logger.LogInformation("Set gold to {Amount} for user {DiscordId}", amount, discordId);
            return Ok(user.Gold);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("create-pack")]
    public async Task<ActionResult> CreatePack([FromBody] PackType pack)
    {
        var result = await _packService.AddAsync(pack);
        if (result.IsFailure)
        {
            return BadRequest(result.Error.Message);
        }
        return Ok(result.Value);
    }

    [HttpPost("set-card-pack")]
    public async Task<ActionResult> SetCardPack([FromQuery] string cardName, [FromQuery] string packName)
    {
        var result = await _cardService.SetCardPackAsync(cardName, packName);
        if (result.IsFailure)
        {
            return result.Error.Code == "CARD_NOT_FOUND" || result.Error.Code == "PACK_NOT_FOUND" 
                ? NotFound(result.Error.Message) 
                : BadRequest(result.Error.Message);
        }
        return Ok();
    }

    [HttpPost("give-card")]
    public async Task<ActionResult> GiveCard([FromQuery] ulong discordId, [FromQuery] string cardName, [FromQuery] int amount = 1)
    {
        var user = await _context.Users.Include(u => u.UserCards).FirstOrDefaultAsync(u => u.DiscordId == discordId);
        if (user == null) return NotFound("User not found");

        var card = await _context.Cards.FirstOrDefaultAsync(c => c.Name == cardName);
        if (card == null) return NotFound("Card not found");

        var userCard = user.UserCards.FirstOrDefault(uc => uc.CardId == card.Id);
        if (userCard != null) userCard.Count += amount;
        else user.UserCards.Add(new UserCard { CardId = card.Id, Count = amount, ObtainedAt = DateTime.UtcNow });

        await _context.SaveChangesAsync();
        _logger.LogInformation("Gave {Amount} of card {CardName} to user {DiscordId}", amount, cardName, discordId);
        return Ok();
    }

    [HttpPost("fix-inventory")]
    public async Task<ActionResult> FixInventory()
    {
        var allUserCards = await _context.UserCards.ToListAsync();
        var duplicates = allUserCards
            .GroupBy(uc => new { uc.UserId, uc.CardId })
            .Where(g => g.Count() > 1)
            .ToList();

        if (duplicates.Count == 0) return Ok("No duplicates found.");

        foreach (var group in duplicates)
        {
            var cards = group.OrderBy(uc => uc.ObtainedAt).ToList();
            var primary = cards.First();
            primary.Count = cards.Sum(c => c.Count);
            foreach (var duplicate in cards.Skip(1)) _context.UserCards.Remove(duplicate);
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Fixed {Count} groups of duplicates", duplicates.Count);
        return Ok($"Fixed {duplicates.Count} groups of duplicates.");
    }

    [HttpPost("add-admin/{discordId}")]
    public async Task<ActionResult> AddAdmin(ulong discordId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.DiscordId == discordId);
        if (user == null) return NotFound();
        user.IsBotAdmin = true;
        await _context.SaveChangesAsync();
        _logger.LogInformation("Added admin: {DiscordId}", discordId);
        return Ok();
    }

    [HttpPost("remove-admin/{discordId}")]
    public async Task<ActionResult> RemoveAdmin(ulong discordId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.DiscordId == discordId);
        if (user == null) return NotFound();
        user.IsBotAdmin = false;
        await _context.SaveChangesAsync();
        _logger.LogInformation("Removed admin: {DiscordId}", discordId);
        return Ok();
    }

    [HttpPost("toggle-pack/{packName}")]
    public async Task<ActionResult> TogglePack(string packName)
    {
        var result = await _packService.ToggleAvailabilityAsync(packName);
        if (result.IsFailure)
        {
            return NotFound(result.Error.Message);
        }
        return Ok(new { packName = result.Value.Name, isAvailable = result.Value.IsAvailable });
    }

    [HttpPut("update-pack/{id}")]
    public async Task<ActionResult> UpdatePack(int id, [FromBody] PackType packUpdate)
    {
        var result = await _packService.UpdateAsync(id, packUpdate);
        if (result.IsFailure)
        {
            return result.Error.Code == "NOT_FOUND" ? NotFound(result.Error.Message) : BadRequest(result.Error.Message);
        }
        return Ok(result.Value);
    }

    [HttpDelete("remove-pack/{id}")]
    public async Task<ActionResult> RemovePack(int id)
    {
        var result = await _packService.DeleteAsync(id);
        if (result.IsFailure)
        {
            return result.Error.Code == "NOT_FOUND" ? NotFound(result.Error.Message) : BadRequest(result.Error.Message);
        }
        return Ok();
    }
}
