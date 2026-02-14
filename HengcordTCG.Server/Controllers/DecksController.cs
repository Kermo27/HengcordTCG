using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HengcordTCG.Shared.Data;
using HengcordTCG.Shared.Models;

namespace HengcordTCG.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DecksController : ControllerBase
{
    private readonly AppDbContext _context;

    public DecksController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("{discordId}")]
    public async Task<ActionResult<Deck?>> GetDeck(ulong discordId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.DiscordId == discordId);
        if (user == null) return NotFound("User not found");

        var deck = await _context.Decks
            .Include(d => d.Commander)
            .Include(d => d.DeckCards)
                .ThenInclude(dc => dc.Card)
            .FirstOrDefaultAsync(d => d.UserId == user.Id);

        if (deck == null) return Ok(null);
        return Ok(deck);
    }

    public record SaveDeckRequest(
        ulong DiscordId,
        string? Name,
        int CommanderId,
        List<int> MainDeckCardIds,
        List<int> CloserCardIds
    );

    [HttpPost("save")]
    public async Task<ActionResult> SaveDeck([FromBody] SaveDeckRequest request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.DiscordId == request.DiscordId);
        if (user == null) return NotFound("User not found");

        // Validate commander
        var commander = await _context.Cards.FindAsync(request.CommanderId);
        if (commander == null) return BadRequest("Commander card not found");
        if (commander.CardType != CardType.Commander)
            return BadRequest($"'{commander.Name}' is not a Commander card");

        // Validate main deck (9 units)
        if (request.MainDeckCardIds.Count != 9)
            return BadRequest($"Main deck must have exactly 9 cards (got {request.MainDeckCardIds.Count})");

        var mainCards = await _context.Cards
            .Where(c => request.MainDeckCardIds.Contains(c.Id))
            .ToListAsync();

        var invalidMain = mainCards.Where(c => c.CardType != CardType.Unit).ToList();
        if (invalidMain.Any())
            return BadRequest($"Main deck cards must be Units. Invalid: {string.Join(", ", invalidMain.Select(c => c.Name))}");

        // Validate closers (3 closers)
        if (request.CloserCardIds.Count != 3)
            return BadRequest($"Closer deck must have exactly 3 cards (got {request.CloserCardIds.Count})");

        var closerCards = await _context.Cards
            .Where(c => request.CloserCardIds.Contains(c.Id))
            .ToListAsync();

        var invalidCloser = closerCards.Where(c => c.CardType != CardType.Closer).ToList();
        if (invalidCloser.Any())
            return BadRequest($"Closer deck cards must be Closers. Invalid: {string.Join(", ", invalidCloser.Select(c => c.Name))}");

        // Verify ownership
        var userCards = await _context.UserCards
            .Where(uc => uc.UserId == user.Id)
            .ToListAsync();

        var allCardIds = new List<int> { request.CommanderId };
        allCardIds.AddRange(request.MainDeckCardIds);
        allCardIds.AddRange(request.CloserCardIds);

        // Count how many of each card is needed
        var needed = allCardIds.GroupBy(id => id).ToDictionary(g => g.Key, g => g.Count());
        foreach (var (cardId, count) in needed)
        {
            var owned = userCards.FirstOrDefault(uc => uc.CardId == cardId);
            if (owned == null || owned.Count < count)
            {
                var card = await _context.Cards.FindAsync(cardId);
                return BadRequest($"You don't own enough copies of '{card?.Name ?? "Unknown"}' (need {count}, have {owned?.Count ?? 0})");
            }
        }

        // Save deck (upsert)
        var existingDeck = await _context.Decks
            .Include(d => d.DeckCards)
            .FirstOrDefaultAsync(d => d.UserId == user.Id);

        if (existingDeck != null)
        {
            existingDeck.CommanderId = request.CommanderId;
            existingDeck.Name = request.Name ?? existingDeck.Name;
            existingDeck.UpdatedAt = DateTime.UtcNow;

            _context.DeckCards.RemoveRange(existingDeck.DeckCards);
            existingDeck.DeckCards = BuildDeckCards(request.MainDeckCardIds, request.CloserCardIds);
        }
        else
        {
            var deck = new Deck
            {
                UserId = user.Id,
                CommanderId = request.CommanderId,
                Name = request.Name ?? "Default",
                DeckCards = BuildDeckCards(request.MainDeckCardIds, request.CloserCardIds)
            };
            _context.Decks.Add(deck);
        }

        await _context.SaveChangesAsync();
        return Ok(new { message = "Deck saved successfully!" });
    }

    private static List<DeckCard> BuildDeckCards(List<int> mainIds, List<int> closerIds)
    {
        var cards = new List<DeckCard>();
        foreach (var id in mainIds)
            cards.Add(new DeckCard { CardId = id, Slot = DeckSlot.MainDeck });
        foreach (var id in closerIds)
            cards.Add(new DeckCard { CardId = id, Slot = DeckSlot.Closer });
        return cards;
    }
}
