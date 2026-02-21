using Microsoft.EntityFrameworkCore;
using HengcordTCG.Shared.Data;
using HengcordTCG.Shared.Models;
using Microsoft.Extensions.Logging;

namespace HengcordTCG.Shared.Services;

public class ShopService
{
    private readonly AppDbContext _db;
    private readonly UserService _userService;
    private readonly ILogger<ShopService> _logger;

    public ShopService(AppDbContext db, UserService userService, ILogger<ShopService> logger)
    {
        _db = db;
        _userService = userService;
        _logger = logger;
    }

    public async Task<(bool success, List<Card> cards, string message)> BuyPackAsync(ulong discordId, string username, string packName = "Base Set")
    {
        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            _logger.LogInformation("Buy pack request from Discord ID {DiscordId}. Pack: {PackName}", discordId, packName);
            
            var user = await _userService.GetOrCreateUserAsync(discordId, username);
            
            var pack = await _db.PackTypes.FirstOrDefaultAsync(p => p.Name == packName);
            
            if (pack == null && packName == "Base Set")
            {
                _logger.LogInformation("Creating default Base Set pack");
                pack = new PackType { Name = "Base Set", Price = 100 };
                _db.PackTypes.Add(pack);
                await _db.SaveChangesAsync();
            }
            else if (pack == null)
            {
                _logger.LogWarning("Pack not found: {PackName}", packName);
                return (false, new List<Card>(), $"Pack '{packName}' not found.");
            }

            if (!pack.IsAvailable)
            {
                _logger.LogWarning("Pack unavailable: {PackName}", packName);
                return (false, new List<Card>(), $"Pack '{packName}' is currently unavailable.");
            }

            if (user.Gold < pack.Price)
            {
                _logger.LogWarning("Insufficient gold for Discord ID {DiscordId}. Required: {Price}, Has: {Gold}", discordId, pack.Price, user.Gold);
                return (false, new List<Card>(), $"Not enough gold! Pack '{pack.Name}' costs {pack.Price}, you have {user.Gold}");
            }

            var availableCards = await _db.Cards
                .Where(c => c.ExclusivePackId == null || c.ExclusivePackId == pack.Id)
                .ToListAsync();

            if (availableCards.Count == 0)
            {
                return (false, new List<Card>(), "This pack is empty!");
            }

            var drawnCards = new List<Card>();
            
            for (var i = 0; i < 3; i++)
            {
                var totalWeight = pack.ChanceCommon + pack.ChanceRare + pack.ChanceLegendary;
                var roll = Random.Shared.Next(1, totalWeight + 1);
                
                Rarity targetRarity;
                var currentThreshold = 0;

                if (roll <= (currentThreshold += pack.ChanceCommon)) targetRarity = Rarity.Common;
                else if (roll <= (currentThreshold += pack.ChanceRare)) targetRarity = Rarity.Rare;
                else targetRarity = Rarity.Legendary;
                
                var specificPool = availableCards.Where(c => c.Rarity == targetRarity).ToList();
                
                if (specificPool.Count == 0 && targetRarity > Rarity.Common)
                {
                    specificPool = availableCards.Where(c => c.Rarity < targetRarity).ToList();
                }
                
                if (specificPool.Count == 0) specificPool = availableCards;

                var card = specificPool[Random.Shared.Next(specificPool.Count)];
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
            await transaction.CommitAsync();

            _logger.LogInformation("Pack purchased successfully for Discord ID {DiscordId}. Pack: {PackName}, Amount: {Amount} gold", discordId, pack.Name, pack.Price);
            return (true, drawnCards, $"Paczka '{pack.Name}' otwarta!");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error buying pack for Discord ID {DiscordId}. Pack: {PackName}", discordId, packName);
            return (false, new List<Card>(), "Error purchasing pack. Please try again.");
        }
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
