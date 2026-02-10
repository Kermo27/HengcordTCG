using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using HengcordTCG.Shared.Data;
using HengcordTCG.Shared.Models;

namespace HengcordTCG.Shared.Services;

public class TradeService
{
    private readonly AppDbContext _db;
    private readonly UserService _userService;

    public TradeService(AppDbContext db, UserService userService)
    {
        _db = db;
        _userService = userService;
    }



    public async Task<(bool success, string message, Trade? trade, TradeContent? offerContent, TradeContent? requestContent)> CreateTradeAsync(
        ulong initiatorId, string initiatorName,
        ulong targetId, string targetName,
        string offerString, string requestString)
    {
        if (initiatorId == targetId) return (false, "Nie możesz wymieniać się sam ze sobą!", null, null, null);

        var initiator = await _userService.GetOrCreateUserAsync(initiatorId, initiatorName);
        var target = await _userService.GetOrCreateUserAsync(targetId, targetName);

        // Parse Offer
        var offerParsed = await ParseTradeStringAsync(offerString);
        if (!offerParsed.success) return (false, $"Błąd w ofercie: {offerParsed.message}", null, null, null);

        // Parse Request
        var requestParsed = await ParseTradeStringAsync(requestString);
        if (!requestParsed.success) return (false, $"Błąd w żądaniu: {requestParsed.message}", null, null, null);

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

        return (true, "Oferta utworzona", trade, offerParsed.content, requestParsed.content);
    }

    public async Task<(bool success, string message)> AcceptTradeAsync(int tradeId, ulong userId)
    {
        var trade = await _db.Trades
            .Include(t => t.Initiator).ThenInclude(u => u.UserCards)
            .Include(t => t.Target).ThenInclude(u => u.UserCards)
            .FirstOrDefaultAsync(t => t.Id == tradeId);

        if (trade == null) return (false, "Nie znaleziono oferty.");
        if (trade.Status != TradeStatus.Pending) return (false, "Oferta nie jest już aktywna.");
        if (trade.Target.DiscordId != userId) return (false, "To nie jest oferta dla Ciebie!");

        var offerCards = JsonSerializer.Deserialize<Dictionary<int, int>>(trade.OfferCardsJson) ?? new();
        var requestCards = JsonSerializer.Deserialize<Dictionary<int, int>>(trade.RequestCardsJson) ?? new();

        // Validate Initiator assets (Gold & Cards)
        if (trade.Initiator.Gold < trade.OfferGold) return (false, $"Inicjator nie ma wystarczająco złota ({trade.OfferGold}).");
        
        foreach (var kvp in offerCards)
        {
            var userCard = trade.Initiator.UserCards.FirstOrDefault(uc => uc.CardId == kvp.Key);
            if (userCard == null || userCard.Count < kvp.Value)
                return (false, $"Inicjator nie ma karty ID:{kvp.Key} w ilości {kvp.Value}.");
        }

        // Validate Target assets
        if (trade.Target.Gold < trade.RequestGold) return (false, $"Nie masz wystarczająco złota ({trade.RequestGold}).");

        foreach (var kvp in requestCards)
        {
            var userCard = trade.Target.UserCards.FirstOrDefault(uc => uc.CardId == kvp.Key);
            if (userCard == null || userCard.Count < kvp.Value)
                return (false, $"Nie masz karty ID:{kvp.Key} w ilości {kvp.Value}.");
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

        return (true, "Wymiana zakończona sukcesem!");
    }

    public async Task<(bool success, string message)> RejectTradeAsync(int tradeId, ulong userId)
    {
        var trade = await _db.Trades.Include(t => t.Target).FirstOrDefaultAsync(t => t.Id == tradeId);
        if (trade == null) return (false, "Nie znaleziono oferty.");
        if (trade.Status != TradeStatus.Pending) return (false, "Oferta nie jest już aktywna.");
        
        // Allow initiator to cancel, or target to reject
        var isInitiator = trade.InitiatorId == (await _userService.GetOrCreateUserAsync(userId, "unknown")).Id; // slight optimization missing here but ok
        // Actually, let's use InitiatorId from trade relation if loaded, but we need UserId not int Id.
        // Let's rely on DiscordId check via DB query to be safe or assuming Initiator is loaded? Not loaded in simple query.
        
        // Better:
        var user = await _userService.GetByDiscordIdAsync(userId);
        if (user == null) return (false, "User not found");

        if (trade.TargetId == user.Id)
        {
            trade.Status = TradeStatus.Rejected;
            await _db.SaveChangesAsync();
            return (true, "Odrzucono ofertę.");
        }
        else if (trade.InitiatorId == user.Id)
        {
            trade.Status = TradeStatus.Cancelled;
            await _db.SaveChangesAsync();
            return (true, "Anulowano ofertę.");
        }

        return (false, "Nie masz uprawnień do tej oferty.");
    }

    private async Task TransferCardsAsync(User fromUser, User toUser, Dictionary<int, int> cardsToTransfer)
    {
        foreach (var kvp in cardsToTransfer)
        {
            var cardId = kvp.Key;
            var count = kvp.Value;

            // Remove from source
            var sourceCard = fromUser.UserCards.First(uc => uc.CardId == cardId);
            sourceCard.Count -= count;
            if (sourceCard.Count == 0) _db.UserCards.Remove(sourceCard);

            // Add to dest
            var destCard = toUser.UserCards.FirstOrDefault(uc => uc.CardId == cardId);
            if (destCard != null)
            {
                destCard.Count += count;
            }
            else
            {
                // We need to fetch Card entity to be safe or just set ID? 
                // Setting ID is enough if we just Add.
                // But let's check if 'toUser.UserCards' collection is loaded fully? 
                // In AcceptTradeAsync we included UserCards, so it should be fine.
                // But if 'destCard' is null, it means it's not in the loaded collection OR not in DB.
                // Since we included it, it's not in DB.
                
                // Caution: we are adding to a tracking collection.
                var newCard = new UserCard
                {
                    UserId = toUser.Id,
                    CardId = cardId,
                    Count = count,
                    ObtainedAt = DateTime.UtcNow
                };
                _db.UserCards.Add(newCard); // Add to context directly or to collection
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
