using Microsoft.EntityFrameworkCore;
using HengcordTCG.Shared.Data;
using HengcordTCG.Shared.Models;

namespace HengcordTCG.Shared.Services;

public class ShopService
{
    private readonly AppDbContext _db;
    private readonly UserService _userService;

    public ShopService(AppDbContext db, UserService userService)
    {
        _db = db;
        _userService = userService;
    }

    public async Task<(bool success, List<Card> cards, string message)> BuyPackAsync(ulong discordId, string username, string packName = "Base Set")
    {
        var user = await _userService.GetOrCreateUserAsync(discordId, username);
        
        var pack = await _db.PackTypes.FirstOrDefaultAsync(p => p.Name == packName);
        
        if (pack == null && packName == "Base Set")
        {
            pack = new PackType { Name = "Base Set", Price = 100 };
            _db.PackTypes.Add(pack);
            await _db.SaveChangesAsync();
        }
        else if (pack == null)
        {
            return (false, new List<Card>(), $"Nie znaleziono paczki o nazwie '{packName}'.");
        }

        if (!pack.IsActive)
        {
            return (false, new List<Card>(), $"Paczka '{packName}' jest obecnie niedostępna w sklepie.");
        }

        if (user.Gold < pack.Price)
        {
            return (false, new List<Card>(), $"Brakuje Ci złota! Koszt paczki '{pack.Name}': {pack.Price}, masz: {user.Gold}");
        }
        
        var poolQuery = _db.Cards.AsQueryable();
        
        if (pack.Name == "Base Set")
        {
             poolQuery = poolQuery.Where(c => c.ExclusivePackId == null || c.ExclusivePackId == pack.Id);
        }
        else
        {
             poolQuery = poolQuery.Where(c => c.ExclusivePackId == pack.Id || c.ExclusivePackId == null);
        }

        var availableCards = await poolQuery.ToListAsync();

        if (availableCards.Count == 0)
        {
            return (false, new List<Card>(), "Ta paczka jest pusta!");
        }

        var random = new Random();
        var drawnCards = new List<Card>();
        
        for (var i = 0; i < 3; i++)
        {
            var totalWeight = pack.ChanceCommon + pack.ChanceRare + pack.ChanceLegendary;
            var roll = random.Next(1, totalWeight + 1);
            
            Rarity targetRarity;
            var currentThreshold = 0;

            if (roll <= (currentThreshold += pack.ChanceCommon)) targetRarity = Rarity.Common;
            else if (roll <= (currentThreshold += pack.ChanceRare)) targetRarity = Rarity.Rare;
            else targetRarity = Rarity.Legendary;
            
            var specificPool = availableCards.Where(c => c.Rarity == targetRarity).ToList();
            
            if (specificPool.Count == 0)
            {
               if (targetRarity > Rarity.Common)
               {
                   specificPool = availableCards.Where(c => c.Rarity < targetRarity).ToList();
               }
            }
            
            if (specificPool.Count == 0) specificPool = availableCards;

            var card = specificPool[random.Next(specificPool.Count)];
            drawnCards.Add(card);
        }
        
        var drawnCounts = drawnCards.GroupBy(c => c.Id)
                                    .ToDictionary(g => g.Key, g => g.Count());

        foreach (var kvp in drawnCounts)
        {
            var cardId = kvp.Key;
            var countToAdd = kvp.Value;
            var cardObj = drawnCards.First(c => c.Id == cardId);
            
            var userCard = await _db.UserCards
                .FirstOrDefaultAsync(uc => uc.UserId == user.Id && uc.CardId == cardId);

            if (userCard != null)
            {
                userCard.Count += countToAdd;
            }
            else
            {
                _db.UserCards.Add(new UserCard
                {
                    UserId = user.Id,
                    CardId = cardId,
                    Card = cardObj,
                    User = user,
                    Count = countToAdd,
                    ObtainedAt = DateTime.UtcNow
                });
            }
        }

        user.Gold -= pack.Price;
        await _db.SaveChangesAsync();

        return (true, drawnCards, $"Paczka '{pack.Name}' otwarta!");
    }

    public async Task<List<UserCard>> GetUserCollectionAsync(ulong discordId)
    {
        return await _db.UserCards
            .Include(uc => uc.Card)
            .Where(uc => uc.User.DiscordId == discordId)
            .OrderByDescending(uc => uc.Count)
            .ToListAsync();
    }
}
