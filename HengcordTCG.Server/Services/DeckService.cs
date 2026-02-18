using HengcordTCG.Shared.Data;
using HengcordTCG.Shared.Models;
using HengcordTCG.Shared.Results;
using HengcordTCG.Shared.DTOs.Decks;
using Microsoft.EntityFrameworkCore;

namespace HengcordTCG.Server.Services;

public interface IDeckService
{
    Task<Result<Deck?>> GetByDiscordIdAsync(ulong discordId);
    Task<Result> SaveAsync(SaveDeckRequest request);
}

public class DeckService : IDeckService
{
    private readonly AppDbContext _context;
    private readonly ILogger<DeckService> _logger;

    public DeckService(AppDbContext context, ILogger<DeckService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<Deck?>> GetByDiscordIdAsync(ulong discordId)
    {
        try
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.DiscordId == discordId);
            if (user == null)
                return Result<Deck?>.Failure("USER_NOT_FOUND", "User not found");

            var deck = await _context.Decks
                .Include(d => d.Commander)
                .Include(d => d.DeckCards)
                    .ThenInclude(dc => dc.Card)
                .FirstOrDefaultAsync(d => d.UserId == user.Id);

            return Result<Deck?>.Success(deck);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get deck for user {DiscordId}", discordId);
            return Result<Deck?>.Failure("DATABASE_ERROR", "Failed to retrieve deck");
        }
    }

    public async Task<Result> SaveAsync(SaveDeckRequest request)
    {
        try
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.DiscordId == request.DiscordId);
            if (user == null)
            {
                _logger.LogWarning("Attempted to save deck for non-existent user {DiscordId}", request.DiscordId);
                return Result.Failure("USER_NOT_FOUND", "User not found");
            }

            var validationResult = await ValidateDeckCardsAsync(request, user.Id);
            if (validationResult.IsFailure)
                return validationResult;

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
            _logger.LogInformation("Saved deck for user {DiscordId}", request.DiscordId);
            
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save deck for user {DiscordId}", request.DiscordId);
            return Result.Failure("DATABASE_ERROR", "Failed to save deck");
        }
    }

    private async Task<Result> ValidateDeckCardsAsync(SaveDeckRequest request, int userId)
    {
        var commander = await _context.Cards.FindAsync(request.CommanderId);
        if (commander == null)
            return Result.Failure("COMMANDER_NOT_FOUND", "Commander card not found");
        
        if (commander.CardType != CardType.Commander)
            return Result.Failure("INVALID_CARD_TYPE", $"'{commander.Name}' is not a Commander card");

        if (request.MainDeckCardIds.Count != 9)
            return Result.Failure("INVALID_MAIN_DECK", $"Main deck must have exactly 9 cards (got {request.MainDeckCardIds.Count})");

        var mainCards = await _context.Cards
            .Where(c => request.MainDeckCardIds.Contains(c.Id))
            .ToListAsync();

        var invalidMain = mainCards.Where(c => c.CardType != CardType.Unit).ToList();
        if (invalidMain.Any())
            return Result.Failure("INVALID_CARD_TYPE", $"Main deck cards must be Units. Invalid: {string.Join(", ", invalidMain.Select(c => c.Name))}");

        if (request.CloserCardIds.Count != 3)
            return Result.Failure("INVALID_CLOSER_DECK", $"Closer deck must have exactly 3 cards (got {request.CloserCardIds.Count})");

        var closerCards = await _context.Cards
            .Where(c => request.CloserCardIds.Contains(c.Id))
            .ToListAsync();

        var invalidCloser = closerCards.Where(c => c.CardType != CardType.Closer).ToList();
        if (invalidCloser.Any())
            return Result.Failure("INVALID_CARD_TYPE", $"Closer deck cards must be Closers. Invalid: {string.Join(", ", invalidCloser.Select(c => c.Name))}");

        var userCards = await _context.UserCards
            .Where(uc => uc.UserId == userId)
            .ToListAsync();

        var allCardIds = new List<int> { request.CommanderId };
        allCardIds.AddRange(request.MainDeckCardIds);
        allCardIds.AddRange(request.CloserCardIds);

        var needed = allCardIds.GroupBy(id => id).ToDictionary(g => g.Key, g => g.Count());
        foreach (var (cardId, count) in needed)
        {
            var owned = userCards.FirstOrDefault(uc => uc.CardId == cardId);
            if (owned == null || owned.Count < count)
            {
                var card = await _context.Cards.FindAsync(cardId);
                return Result.Failure("INSUFFICIENT_CARDS", $"You don't own enough copies of '{card?.Name ?? "Unknown"}' (need {count}, have {owned?.Count ?? 0})");
            }
        }

        return Result.Success();
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
