using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using HengcordTCG.Shared.Services;
using HengcordTCG.Shared.Models;
using HengcordTCG.Tests.Helpers;

namespace HengcordTCG.Tests.Services;

public class TradeServiceTests
{
    private (TradeService trade, UserService user, Shared.Data.AppDbContext db) CreateServices()
    {
        var db = TestDbContextFactory.Create();
        var userLogger = new Mock<ILogger<UserService>>();
        var tradeLogger = new Mock<ILogger<TradeService>>();
        var userService = new UserService(db, userLogger.Object);
        var tradeService = new TradeService(db, userService, tradeLogger.Object);
        return (tradeService, userService, db);
    }

    private async Task SeedUsersWithCards(Shared.Data.AppDbContext db, UserService user)
    {
        var card = new Card { Name = "Smoczy Miecz", Rarity = Rarity.Rare, Attack = 50, Defense = 30 };
        db.Cards.Add(card);
        await db.SaveChangesAsync();

        var initiator = await user.GetOrCreateUserAsync(200000000000000001, "Initiator");
        initiator.Gold = 1000;

        var target = await user.GetOrCreateUserAsync(200000000000000002, "Target");
        target.Gold = 500;

        db.UserCards.Add(new UserCard
        {
            UserId = initiator.Id,
            CardId = card.Id,
            Count = 5,
            ObtainedAt = DateTime.UtcNow
        });

        db.UserCards.Add(new UserCard
        {
            UserId = target.Id,
            CardId = card.Id,
            Count = 3,
            ObtainedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task CreateTrade_ValidData_Success()
    {
        var (trade, user, db) = CreateServices();
        using (db)
        {
            await SeedUsersWithCards(db, user);
            var (success, _, tradeObj, _, _) = await trade.CreateTradeAsync(
                200000000000000001, "Initiator",
                200000000000000002, "Target",
                "Gold: 100", "Smoczy Miecz x1");

            success.Should().BeTrue();
            tradeObj.Should().NotBeNull();
            tradeObj!.Status.Should().Be(TradeStatus.Pending);
        }
    }

    [Fact]
    public async Task CreateTrade_SelfTrade_Fails()
    {
        var (trade, _, db) = CreateServices();
        using (db)
        {
            var (success, message, _, _, _) = await trade.CreateTradeAsync(
                210000000000000001, "User",
                210000000000000001, "User",
                "Gold: 100", "Gold: 50");

            success.Should().BeFalse();
            message.Should().Contain("yourself");
        }
    }

    [Fact]
    public async Task RejectTrade_SetsStatus()
    {
        var (trade, user, db) = CreateServices();
        using (db)
        {
            await SeedUsersWithCards(db, user);
            var (_, _, tradeObj, _, _) = await trade.CreateTradeAsync(
                200000000000000001, "Initiator",
                200000000000000002, "Target",
                "Gold: 100", "Gold: 50");

            var (success, _) = await trade.RejectTradeAsync(tradeObj!.Id, 200000000000000002);

            success.Should().BeTrue();
            var rejected = await db.Trades.FindAsync(tradeObj.Id);
            rejected!.Status.Should().Be(TradeStatus.Rejected);
        }
    }

    [Fact]
    public async Task AcceptTrade_TransfersCardsAndGold()
    {
        var (trade, user, db) = CreateServices();
        using (db)
        {
            await SeedUsersWithCards(db, user);
            var card = await db.Cards.FirstAsync();

            var (_, _, tradeObj, _, _) = await trade.CreateTradeAsync(
                200000000000000001, "Initiator",
                200000000000000002, "Target",
                $"{card.Name} x1, Gold: 100", "Gold: 50");

            var (success, _) = await trade.AcceptTradeAsync(tradeObj!.Id, 200000000000000002);

            success.Should().BeTrue();

            var initiatorAfter = await user.GetByDiscordIdAsync(200000000000000001);
            var targetAfter = await user.GetByDiscordIdAsync(200000000000000002);

            initiatorAfter!.Gold.Should().Be(950); // 1000 - 100 + 50
            targetAfter!.Gold.Should().Be(550); // 500 + 100 - 50

            var initiatorCards = await db.UserCards.FirstOrDefaultAsync(uc => uc.UserId == initiatorAfter.Id && uc.CardId == card.Id);
            var targetCards = await db.UserCards.FirstOrDefaultAsync(uc => uc.UserId == targetAfter.Id && uc.CardId == card.Id);

            initiatorCards!.Count.Should().Be(4);
            targetCards!.Count.Should().Be(4);
        }
    }

    [Fact]
    public async Task AcceptTrade_InsufficientCards_Fails()
    {
        var (trade, user, db) = CreateServices();
        using (db)
        {
            await SeedUsersWithCards(db, user);
            var card = await db.Cards.FirstAsync();

            var (_, _, tradeObj, _, _) = await trade.CreateTradeAsync(
                200000000000000001, "Initiator",
                200000000000000002, "Target",
                $"{card.Name} x10", "Gold: 50");

            var (success, message) = await trade.AcceptTradeAsync(tradeObj!.Id, 200000000000000002);

            success.Should().BeFalse();
            message.Should().Contain("does not have card");
        }
    }

    [Fact]
    public async Task AcceptTrade_TargetInsufficientGold_Fails()
    {
        var (trade, user, db) = CreateServices();
        using (db)
        {
            await SeedUsersWithCards(db, user);

            var (_, _, tradeObj, _, _) = await trade.CreateTradeAsync(
                200000000000000001, "Initiator",
                200000000000000002, "Target",
                "Gold: 10", "Gold: 1000");

            var (success, message) = await trade.AcceptTradeAsync(tradeObj!.Id, 200000000000000002);

            success.Should().BeFalse();
            message.Should().Contain("enough gold");
        }
    }

    [Fact]
    public async Task ParseTradeString_MultipleCards_Success()
    {
        var (trade, user, db) = CreateServices();
        using (db)
        {
            await SeedUsersWithCards(db, user);
            var (success, _, _, content, _) = await trade.CreateTradeAsync(
                200000000000000001, "Initiator",
                200000000000000002, "Target",
                "Smoczy Miecz x2, Gold: 100", "Gold: 50");

            success.Should().BeTrue();
            content!.Gold.Should().Be(100);
            content.Cards.Should().HaveCount(1);
        }
    }
}
