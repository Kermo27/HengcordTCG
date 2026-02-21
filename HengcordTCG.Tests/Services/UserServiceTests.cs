using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using HengcordTCG.Shared.Services;
using HengcordTCG.Tests.Helpers;

namespace HengcordTCG.Tests.Services;

public class UserServiceTests
{
    private UserService CreateService(Shared.Data.AppDbContext db)
    {
        var logger = new Mock<ILogger<UserService>>();
        return new UserService(db, logger.Object);
    }

    [Fact]
    public async Task GetOrCreate_NewUser_CreatesWithCorrectData()
    {
        using var db = TestDbContextFactory.Create();
        var svc = CreateService(db);

        var user = await svc.GetOrCreateUserAsync(123456789012345678, "TestUser");

        user.Should().NotBeNull();
        user.DiscordId.Should().Be(123456789012345678);
        user.Username.Should().Be("TestUser");
        user.Gold.Should().Be(0);
    }

    [Fact]
    public async Task GetOrCreate_ExistingUser_UpdatesLastSeen()
    {
        using var db = TestDbContextFactory.Create();
        var svc = CreateService(db);

        var user1 = await svc.GetOrCreateUserAsync(111111111111111111, "User1");
        var oldLastSeen = user1.LastSeen;

        await Task.Delay(50);
        var user2 = await svc.GetOrCreateUserAsync(111111111111111111, "User1");

        user2.Id.Should().Be(user1.Id);
        user2.LastSeen.Should().BeOnOrAfter(oldLastSeen);
    }

    [Fact]
    public async Task GetOrCreate_DiscordIdAsUsername_SavesAsSafe()
    {
        using var db = TestDbContextFactory.Create();
        var svc = CreateService(db);

        // A 18-digit number looks like a Discord ID
        var user = await svc.GetOrCreateUserAsync(222222222222222222, "222222222222222222");

        user.Username.Should().StartWith("User_");
        user.Username.Should().NotBe("222222222222222222");
    }

    [Fact]
    public async Task GetOrCreate_CorruptedUsername_SelfHeals()
    {
        using var db = TestDbContextFactory.Create();
        var svc = CreateService(db);

        // First call creates with discord ID as username (simulating corruption)
        var user1 = await svc.GetOrCreateUserAsync(333333333333333333, "333333333333333333");
        user1.Username.Should().StartWith("User_");

        // Second call with a real username should heal
        var user2 = await svc.GetOrCreateUserAsync(333333333333333333, "RealName");
        user2.Username.Should().Be("RealName");
    }

    [Fact]
    public async Task ClaimDaily_FirstClaim_Success()
    {
        using var db = TestDbContextFactory.Create();
        var svc = CreateService(db);

        var (success, amount, timeRemaining) = await svc.ClaimDailyAsync(444444444444444444, "Claimer");

        success.Should().BeTrue();
        amount.Should().BeInRange(100, 500);
        timeRemaining.Should().BeNull();
    }

    [Fact]
    public async Task ClaimDaily_Cooldown_ReturnsFalseWithTime()
    {
        using var db = TestDbContextFactory.Create();
        var svc = CreateService(db);

        await svc.ClaimDailyAsync(555555555555555555, "Claimer");

        var (success, _, timeRemaining) = await svc.ClaimDailyAsync(555555555555555555, "Claimer");

        success.Should().BeFalse();
        timeRemaining.Should().NotBeNull();
        timeRemaining!.Value.TotalHours.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ClaimDaily_GoldInRange_100to500()
    {
        using var db = TestDbContextFactory.Create();
        var svc = CreateService(db);
        var amounts = new List<long>();

        // Run multiple claims with different users to test range
        for (ulong i = 0; i < 20; i++)
        {
            var freshDb = TestDbContextFactory.Create();
            var freshSvc = CreateService(freshDb);
            var (_, amount, _) = await freshSvc.ClaimDailyAsync(600000000000000000 + i, $"User{i}");
            amounts.Add(amount);
            freshDb.Dispose();
        }

        amounts.Should().AllSatisfy(a => a.Should().BeInRange(100, 500));
    }

    [Fact]
    public async Task AddGold_AddsToBalance()
    {
        using var db = TestDbContextFactory.Create();
        var svc = CreateService(db);

        await svc.GetOrCreateUserAsync(777777777777777777, "GoldUser");
        await svc.AddGoldAsync(777777777777777777, 500);

        var user = await svc.GetByDiscordIdAsync(777777777777777777);
        user!.Gold.Should().Be(500);
    }

    [Fact]
    public async Task AddGold_NonExistentUser_NoOp()
    {
        using var db = TestDbContextFactory.Create();
        var svc = CreateService(db);

        // Should not throw
        await svc.AddGoldAsync(999999999999999999, 500);
    }

    [Fact]
    public async Task GetByDiscordId_Exists_Returns()
    {
        using var db = TestDbContextFactory.Create();
        var svc = CreateService(db);

        await svc.GetOrCreateUserAsync(888888888888888888, "Existing");
        var user = await svc.GetByDiscordIdAsync(888888888888888888);

        user.Should().NotBeNull();
        user!.Username.Should().Be("Existing");
    }

    [Fact]
    public async Task GetByDiscordId_NotExists_ReturnsNull()
    {
        using var db = TestDbContextFactory.Create();
        var svc = CreateService(db);

        var user = await svc.GetByDiscordIdAsync(111111111111111112);
        user.Should().BeNull();
    }

    [Fact]
    public async Task GetOrCreate_ValidUsername_UpdatesOnSubsequentCalls()
    {
        using var db = TestDbContextFactory.Create();
        var svc = CreateService(db);

        await svc.GetOrCreateUserAsync(100000000000000001, "OldName");
        var user = await svc.GetOrCreateUserAsync(100000000000000001, "NewName");

        user.Username.Should().Be("NewName");
    }
}
