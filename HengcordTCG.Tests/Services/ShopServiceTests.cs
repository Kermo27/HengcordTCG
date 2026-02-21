using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using HengcordTCG.Shared.Services;
using HengcordTCG.Shared.Models;
using HengcordTCG.Tests.Helpers;

namespace HengcordTCG.Tests.Services;

public class ShopServiceTests
{
    private (ShopService shop, UserService user, Shared.Data.AppDbContext db) CreateServices()
    {
        var db = TestDbContextFactory.Create();
        var userLogger = new Mock<ILogger<UserService>>();
        var shopLogger = new Mock<ILogger<ShopService>>();
        var userService = new UserService(db, userLogger.Object);
        var shopService = new ShopService(db, userService, shopLogger.Object);
        return (shopService, userService, db);
    }

    private async Task SeedPack(Shared.Data.AppDbContext db, string name = "Test Pack", int price = 100, bool available = true)
    {
        var pack = new PackType
        {
            Name = name,
            Price = price,
            IsAvailable = available,
            ChanceCommon = 60,
            ChanceRare = 35,
            ChanceLegendary = 5
        };
        db.PackTypes.Add(pack);
        await db.SaveChangesAsync();
    }

    private async Task SeedCards(Shared.Data.AppDbContext db, int count = 5)
    {
        for (int i = 1; i <= count; i++)
        {
            db.Cards.Add(new Card
            {
                Name = $"Card_{i}",
                Attack = i * 10,
                Defense = i * 5,
                Rarity = i <= 3 ? Rarity.Common : (i == 4 ? Rarity.Rare : Rarity.Legendary)
            });
        }
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task BuyPack_Success_DeductsGoldAndAddsCards()
    {
        var (shop, user, db) = CreateServices();
        using (db)
        {
            await SeedPack(db, "Test Pack", 100);
            await SeedCards(db);
            var u = await user.GetOrCreateUserAsync(100000000000000001, "Buyer");
            u.Gold = 500;
            await db.SaveChangesAsync();

            var (success, cards, message) = await shop.BuyPackAsync(100000000000000001, "Buyer", "Test Pack");

            success.Should().BeTrue();
            cards.Should().HaveCount(3);
            var updated = await user.GetByDiscordIdAsync(100000000000000001);
            updated!.Gold.Should().Be(400);
        }
    }

    [Fact]
    public async Task BuyPack_InsufficientGold_Fails()
    {
        var (shop, user, db) = CreateServices();
        using (db)
        {
            await SeedPack(db, "Expensive", 1000);
            await SeedCards(db);
            await user.GetOrCreateUserAsync(100000000000000002, "Broke");

            var (success, cards, message) = await shop.BuyPackAsync(100000000000000002, "Broke", "Expensive");

            success.Should().BeFalse();
            message.Should().Contain("Not enough gold");
        }
    }

    [Fact]
    public async Task BuyPack_PackNotFound_Fails()
    {
        var (shop, _, db) = CreateServices();
        using (db)
        {
            var (success, _, message) = await shop.BuyPackAsync(100000000000000003, "User", "NonExistent Pack");

            success.Should().BeFalse();
            message.Should().Contain("not found");
        }
    }

    [Fact]
    public async Task BuyPack_PackUnavailable_Fails()
    {
        var (shop, user, db) = CreateServices();
        using (db)
        {
            await SeedPack(db, "Unavailable", 100, available: false);
            await SeedCards(db);
            var u = await user.GetOrCreateUserAsync(100000000000000004, "User");
            u.Gold = 500;
            await db.SaveChangesAsync();

            var (success, _, message) = await shop.BuyPackAsync(100000000000000004, "User", "Unavailable");

            success.Should().BeFalse();
            message.Should().Contain("unavailable");
        }
    }

    [Fact]
    public async Task BuyPack_NoCards_Fails()
    {
        var (shop, user, db) = CreateServices();
        using (db)
        {
            await SeedPack(db, "Empty Pack", 50);
            // No cards seeded
            var u = await user.GetOrCreateUserAsync(100000000000000005, "User");
            u.Gold = 500;
            await db.SaveChangesAsync();

            var (success, _, message) = await shop.BuyPackAsync(100000000000000005, "User", "Empty Pack");

            success.Should().BeFalse();
            message.Should().Contain("empty");
        }
    }

    [Fact]
    public async Task BuyPack_BaseSet_AutoCreated()
    {
        var (shop, user, db) = CreateServices();
        using (db)
        {
            await SeedCards(db);
            var u = await user.GetOrCreateUserAsync(100000000000000006, "User");
            u.Gold = 500;
            await db.SaveChangesAsync();

            var (success, cards, _) = await shop.BuyPackAsync(100000000000000006, "User", "Base Set");

            success.Should().BeTrue();
            cards.Should().HaveCount(3);
        }
    }

    [Fact]
    public async Task BuyPack_DuplicateCard_IncrementsCount()
    {
        var (shop, user, db) = CreateServices();
        using (db)
        {
            await SeedPack(db, "Single Card Pack", 50);
            // Add only one card so all 3 draws are the same
            db.Cards.Add(new Card { Name = "Only Card", Rarity = Rarity.Common });
            await db.SaveChangesAsync();

            var u = await user.GetOrCreateUserAsync(100000000000000007, "User");
            u.Gold = 500;
            await db.SaveChangesAsync();

            var (success, _, _) = await shop.BuyPackAsync(100000000000000007, "User", "Single Card Pack");

            success.Should().BeTrue();
            var collection = await shop.GetUserCollectionAsync(100000000000000007);
            collection.Should().HaveCount(1);
            collection[0].Count.Should().Be(3);
        }
    }

    [Fact]
    public async Task BuyPack_RarityDistribution_Respects()
    {
        var (shop, user, db) = CreateServices();
        using (db)
        {
            var pack = new PackType
            {
                Name = "Legendary Pack",
                Price = 100,
                ChanceCommon = 0,
                ChanceRare = 0,
                ChanceLegendary = 100
            };
            db.PackTypes.Add(pack);
            db.Cards.Add(new Card { Name = "Legendary", Rarity = Rarity.Legendary });
            db.Cards.Add(new Card { Name = "Common", Rarity = Rarity.Common });
            await db.SaveChangesAsync();

            var u = await user.GetOrCreateUserAsync(100000000000000009, "User");
            u.Gold = 500;
            await db.SaveChangesAsync();

            var (success, cards, _) = await shop.BuyPackAsync(100000000000000009, "User", "Legendary Pack");

            success.Should().BeTrue();
            cards.Should().AllSatisfy(c => c.Rarity.Should().Be(Rarity.Legendary));
        }
    }

    [Fact]
    public async Task GetUserCollection_OrdersByCountDescending()
    {
        var (shop, user, db) = CreateServices();
        using (db)
        {
            var u = await user.GetOrCreateUserAsync(100000000000000010, "Collector");
            var card1 = new Card { Name = "Card1" };
            var card2 = new Card { Name = "Card2" };
            db.Cards.AddRange(card1, card2);
            await db.SaveChangesAsync();

            db.UserCards.Add(new UserCard { UserId = u.Id, CardId = card1.Id, Count = 1, ObtainedAt = DateTime.UtcNow });
            db.UserCards.Add(new UserCard { UserId = u.Id, CardId = card2.Id, Count = 5, ObtainedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();

            var collection = await shop.GetUserCollectionAsync(100000000000000010);
            
            collection.Should().HaveCount(2);
            collection[0].CardId.Should().Be(card2.Id);
            collection[1].CardId.Should().Be(card1.Id);
        }
    }
}
