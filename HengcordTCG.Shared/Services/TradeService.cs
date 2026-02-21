using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using HengcordTCG.Shared.Data;
using HengcordTCG.Shared.Models;

namespace HengcordTCG.Shared.Services;

public class TradeService
{
    private readonly AppDbContext _db;
    private readonly UserService _userService;
    private readonly ILogger<TradeService> _logger;

    public TradeService(AppDbContext db, UserService userService, ILogger<TradeService> logger)
    {
        _db = db;
        _userService = userService;
        _logger = logger;
    }


    public async Task<(bool success, string message, Trade? trade, TradeContent? offerContent, TradeContent? requestContent)> CreateTradeAsync(
        ulong initiatorId, string initiatorName,
        ulong targetId, string targetName,
        string offerString, string requestString)
    {
        if (initiatorId == targetId) return (false, "You cannot trade with yourself!", null, null, null);

        var initiator = await _userService.GetOrCreateUserAsync(initiatorId, initiatorName);
        var target = await _userService.GetOrCreateUserAsync(targetId, targetName);

        // Parse Offer
        var offerParsed = await ParseTradeStringAsync(offerString);
        if (!offerParsed.success) return (false, $"Offer error: {offerParsed.message}", null, null, null);

        // Parse Request
        var requestParsed = await ParseTradeStringAsync(requestString);
        if (!requestParsed.success) return (false, $"Request error: {requestParsed.message}", null, null, null);

        // Create Trade logic object
        var trade = new Trade
        {
            InitiatorId = initiator.Id,
            TargetId = target.Id,
            OfferGold = offerParsed.content.Gold,
            RequestGold = requestParsed.content.Gold,
            OfferCardsJson = JsonSerializer.Serialize(offerParsed.content.Cards),
            RequestCardsJson = JsonSerializer.Serialize(requestParsed.content.Cards),
            Status = TradeStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _db.Trades.Add(trade);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Trade {TradeId} created: {InitiatorId} → {TargetId}", trade.Id, initiatorId, targetId);
        return (true, "Trade offer created", trade, offerParsed.content, requestParsed.content);
    }

    public async Task<(bool success, string message)> AcceptTradeAsync(int tradeId, ulong userId)
    {
        using (var transaction = _db.Database.BeginTransaction())
        {
            try
            {
                var trade = await _db.Trades
                    .Include(t => t.Initiator).ThenInclude(u => u.UserCards)
                    .Include(t => t.Target).ThenInclude(u => u.UserCards)
                    .FirstOrDefaultAsync(t => t.Id == tradeId);

                if (trade == null)
                {
                    await transaction.RollbackAsync();
                    return (false, "Trade offer not found.");
                }

                if (trade.Status != TradeStatus.Pending)
                {
                    await transaction.RollbackAsync();
                    return (false, "This offer is no longer active.");
                }

                if (trade.Target.DiscordId != userId)
                {
                    await transaction.RollbackAsync();
                    return (false, "This offer is not for you!");
                }

                var offerCards = JsonSerializer.Deserialize<Dictionary<int, int>>(trade.OfferCardsJson) ?? new();
                var requestCards = JsonSerializer.Deserialize<Dictionary<int, int>>(trade.RequestCardsJson) ?? new();

                // Validate Initiator assets (Gold & Cards)
                if (trade.Initiator.Gold < trade.OfferGold)
                {
                    await transaction.RollbackAsync();
                    return (false, $"Initiator does not have enough gold ({trade.OfferGold}).");
                }

                foreach (var kvp in offerCards)
                {
                    var userCard = trade.Initiator.UserCards.FirstOrDefault(uc => uc.CardId == kvp.Key);
                    if (userCard == null || userCard.Count < kvp.Value)
                    {
                        await transaction.RollbackAsync();
                        return (false, $"Initiator does not have card ID:{kvp.Key} in quantity {kvp.Value}.");
                    }
                }

                // Validate Target assets
                if (trade.Target.Gold < trade.RequestGold)
                {
                    await transaction.RollbackAsync();
                    return (false, $"You don't have enough gold ({trade.RequestGold}).");
                }

                foreach (var kvp in requestCards)
                {
                    var userCard = trade.Target.UserCards.FirstOrDefault(uc => uc.CardId == kvp.Key);
                    if (userCard == null || userCard.Count < kvp.Value)
                    {
                        await transaction.RollbackAsync();
                        return (false, $"You don't have card ID:{kvp.Key} in quantity {kvp.Value}.");
                    }
                }

                // EXECUTE TRANSFER

                // 1. Move Gold
                trade.Initiator.Gold -= trade.OfferGold;
                trade.Target.Gold += trade.OfferGold;

                trade.Target.Gold -= trade.RequestGold;
                trade.Initiator.Gold += trade.RequestGold;

                // 2. Move Cards (Initiator -> Target)
                await TransferCardsAsync(trade.Initiator, trade.Target, offerCards);

                // 3. Move Cards (Target -> Initiator)
                await TransferCardsAsync(trade.Target, trade.Initiator, requestCards);

                trade.Status = TradeStatus.Accepted;
                await _db.SaveChangesAsync();

                await transaction.CommitAsync();
                _logger.LogInformation("Trade {TradeId} accepted by {UserId}", tradeId, userId);
                return (true, "Trade completed successfully!");
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }

    public async Task<(bool success, string message)> RejectTradeAsync(int tradeId, ulong userId)
    {
        var trade = await _db.Trades.Include(t => t.Target).Include(t => t.Initiator).FirstOrDefaultAsync(t => t.Id == tradeId);
        if (trade == null) return (false, "Trade offer not found.");
        if (trade.Status != TradeStatus.Pending) return (false, "This offer is no longer active.");
        
        // Check if user is either initiator or target
        if (trade.Initiator.DiscordId == userId)
        {
            trade.Status = TradeStatus.Cancelled;
            await _db.SaveChangesAsync();
            _logger.LogInformation("Trade {TradeId} cancelled by initiator {UserId}", tradeId, userId);
            return (true, "Trade cancelled.");
        }
        else if (trade.Target.DiscordId == userId)
        {
            trade.Status = TradeStatus.Rejected;
            await _db.SaveChangesAsync();
            _logger.LogInformation("Trade {TradeId} rejected by target {UserId}", tradeId, userId);
            return (true, "Trade rejected.");
        }

        return (false, "You don't have permission for this trade.");
    }

    private async Task TransferCardsAsync(User fromUser, User toUser, Dictionary<int, int> cardsToTransfer)
    {
        foreach (var kvp in cardsToTransfer)
        {
            var cardId = kvp.Key;
            var count = kvp.Value;

            var sourceCard = fromUser.UserCards.First(uc => uc.CardId == cardId);
            sourceCard.Count -= count;
            if (sourceCard.Count == 0) _db.UserCards.Remove(sourceCard);

            var destCard = toUser.UserCards.FirstOrDefault(uc => uc.CardId == cardId);
            if (destCard != null)
            {
                destCard.Count += count;
            }
            else
            {
                _db.UserCards.Add(new UserCard
                {
                    UserId = toUser.Id,
                    CardId = cardId,
                    Count = count,
                    ObtainedAt = DateTime.UtcNow
                });
            }
        }
    }

    // Helper: Parse "Smoczy Miecz x2, Gold: 100"
    private async Task<(bool success, TradeContent content, string message)> ParseTradeStringAsync(string input)
    {
        var content = new TradeContent();
        if (string.IsNullOrWhiteSpace(input)) return (true, content, ""); // Empty is valid (e.g. gift)

        var parts = input.Split(',');
        foreach (var part in parts)
        {
            var p = part.Trim();
            if (string.IsNullOrEmpty(p)) continue;

            // Check gold
            if (p.StartsWith("Gold:", StringComparison.OrdinalIgnoreCase) || 
                p.StartsWith("Złoto:", StringComparison.OrdinalIgnoreCase) ||
                p.StartsWith("Gold ", StringComparison.OrdinalIgnoreCase) ||
                p.StartsWith("Zloto ", StringComparison.OrdinalIgnoreCase)) // relaxed parsing
            {
                var goldStr = p.Split(new[] { ':', ' ' }, StringSplitOptions.RemoveEmptyEntries).Last();
                if (long.TryParse(goldStr, out long gold))
                {
                    content.Gold += gold;
                    continue;
                }
            }

            // Check card: "Name xCount" or just "Name" (count 1)
            var xIndex = p.LastIndexOf('x');
            // Ensure x is followed by digits only
            if (xIndex != -1 && xIndex < p.Length - 1 && char.IsDigit(p[xIndex + 1]))
            {
                var name = p.Substring(0, xIndex).Trim();
                var countStr = p.Substring(xIndex + 1).Trim();
                if (int.TryParse(countStr, out int count))
                {
                    if (!await AddCardToContent(content, name, count)) return (false, content, $"Nie znaleziono karty: {name}");
                    continue;
                }
            }
            
            // Assume just name = 1 count
            if (!await AddCardToContent(content, p, 1)) return (false, content, $"Nie znaleziono karty: {p}");
        }

        return (true, content, "");
    }

    private async Task<bool> AddCardToContent(TradeContent content, string cardName, int count)
    {
        // Try exact match first
        var card = await _db.Cards.FirstOrDefaultAsync(c => c.Name.ToLower() == cardName.ToLower());
        if (card == null) return false;

        if (content.Cards.ContainsKey(card.Id)) content.Cards[card.Id] += count;
        else content.Cards[card.Id] = count;
        
        if (content.CardNames.ContainsKey(card.Name)) content.CardNames[card.Name] += count;
        else content.CardNames[card.Name] = count;
        
        return true;
    }
}
