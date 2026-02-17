using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HengcordTCG.Shared.Data;
using HengcordTCG.Shared.Models;
using HengcordTCG.Server.Extensions;

namespace HengcordTCG.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _context;

    public AdminController(AppDbContext context)
    {
        _context = context;
    }

    [HttpPost("add-card")]
    public async Task<ActionResult> AddCard(Card card)
    {
        var existing = await _context.Cards.FirstOrDefaultAsync(c => c.Name == card.Name);
        if (existing != null) return BadRequest("Karta już istnieje.");
        
        _context.Cards.Add(card);
        await _context.SaveChangesAsync();
        return Ok(card);
    }

    [HttpPut("update-card/{id}")]
    public async Task<ActionResult> UpdateCard(int id, [FromBody] Card card)
    {
        var existing = await _context.Cards.FindAsync(id);
        if (existing == null) return NotFound("Card not found");

        existing.Name = card.Name;
        existing.Attack = card.Attack;
        existing.Defense = card.Defense;
        existing.Rarity = card.Rarity;
        existing.ImagePath = card.ImagePath;
        existing.ExclusivePackId = card.ExclusivePackId;
        existing.CardType = card.CardType;
        existing.LightCost = card.LightCost;
        existing.Health = card.Health;
        existing.Speed = card.Speed;
        existing.MinDamage = card.MinDamage;
        existing.MaxDamage = card.MaxDamage;
        existing.CounterStrike = card.CounterStrike;
        existing.AbilityText = card.AbilityText;
        existing.AbilityId = card.AbilityId;

        await _context.SaveChangesAsync();
        return Ok(existing);
    }

    [HttpDelete("remove-card/{name}")]
    public async Task<ActionResult> RemoveCard(string name)
    {
        var card = await _context.Cards.FirstOrDefaultAsync(c => c.Name == name);
        if (card == null) return NotFound("Nie znaleziono karty.");
        
        _context.Cards.Remove(card);
        await _context.SaveChangesAsync();
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
        var user = await _context.Users.FirstOrDefaultAsync(u => u.DiscordId == discordId);
        if (user == null) return NotFound();
        
        user.Gold = amount;
        await _context.SaveChangesAsync();
        return Ok(user.Gold);
    }

    [HttpPost("create-pack")]
    public async Task<ActionResult> CreatePack([FromBody] PackType pack)
    {
        _context.PackTypes.Add(pack);
        await _context.SaveChangesAsync();
        return Ok(pack);
    }

    [HttpPost("set-card-pack")]
    public async Task<ActionResult> SetCardPack([FromQuery] string cardName, [FromQuery] string packName)
    {
        var card = await _context.Cards.FirstOrDefaultAsync(c => c.Name == cardName);
        if (card == null) return NotFound("Karta nie istnieje.");

        if (packName.ToLower() == "null")
        {
            card.ExclusivePackId = null;
        }
        else
        {
            var pack = await _context.PackTypes.FirstOrDefaultAsync(p => p.Name == packName);
            if (pack == null) return NotFound("Paczka nie istnieje.");
            card.ExclusivePackId = pack.Id;
        }

        await _context.SaveChangesAsync();
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
        return Ok($"Fixed {duplicates.Count} groups of duplicates.");
    }

    [HttpPost("add-admin/{discordId}")]
    public async Task<ActionResult> AddAdmin(ulong discordId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.DiscordId == discordId);
        if (user == null) return NotFound();
        user.IsBotAdmin = true;
        await _context.SaveChangesAsync();
        return Ok();
    }

    [HttpPost("remove-admin/{discordId}")]
    public async Task<ActionResult> RemoveAdmin(ulong discordId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.DiscordId == discordId);
        if (user == null) return NotFound();
        user.IsBotAdmin = false;
        await _context.SaveChangesAsync();
        return Ok();
    }

    [HttpPost("toggle-pack/{packName}")]
    public async Task<ActionResult> TogglePack(string packName)
    {
        var pack = await _context.PackTypes.FirstOrDefaultAsync(p => p.Name == packName);
        if (pack == null) return NotFound("Paczka nie istnieje.");
        
        pack.IsAvailable = !pack.IsAvailable;
        await _context.SaveChangesAsync();
        return Ok(new { packName, isAvailable = pack.IsAvailable });
    }

    [HttpPut("update-pack/{id}")]
    public async Task<ActionResult> UpdatePack(int id, [FromBody] PackType packUpdate)
    {
        var pack = await _context.PackTypes.FindAsync(id);
        if (pack == null) return NotFound("Paczka nie istnieje.");

        pack.Name = packUpdate.Name;
        pack.Price = packUpdate.Price;
        pack.ChanceCommon = packUpdate.ChanceCommon;
        pack.ChanceRare = packUpdate.ChanceRare;
        pack.ChanceLegendary = packUpdate.ChanceLegendary;
        pack.IsAvailable = packUpdate.IsAvailable;

        await _context.SaveChangesAsync();
        return Ok(pack);
    }

    [HttpDelete("remove-pack/{id}")]
    public async Task<ActionResult> RemovePack(int id)
    {
        var pack = await _context.PackTypes.FindAsync(id);
        if (pack == null) return NotFound("Paczka nie istnieje.");
        
        _context.PackTypes.Remove(pack);
        await _context.SaveChangesAsync();
        return Ok();
    }
}
