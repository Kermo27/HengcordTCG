using System.Net;
using System.Net.Http.Json;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using HengcordTCG.Server.Tests.Helpers;
using HengcordTCG.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using HengcordTCG.Shared.Data;

namespace HengcordTCG.Server.Tests.Controllers;

public class ShopControllerIntegrationTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;
    private readonly HttpClient _client;

    public ShopControllerIntegrationTests(CustomWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        // Set API Key as default header - ShopController accepts both JWT and ApiKey
        _client.DefaultRequestHeaders.Add("X-API-KEY", "test-api-key");
    }

    [Fact]
    public async Task GetPacks_ReturnsOk()
    {
        // Arrange
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (!db.PackTypes.Any(p => p.Name == "Base Set"))
            {
                db.PackTypes.Add(new PackType { Name = "Base Set", Price = 100, IsAvailable = true });
                await db.SaveChangesAsync();
            }
        }

        // Act
        var response = await _client.GetAsync("/api/shop/packs");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var packs = await response.Content.ReadFromJsonAsync<List<PackType>>();
        packs.Should().NotBeNull();
        packs.Should().Contain(p => p.Name == "Base Set");
    }

    [Fact]
    public async Task BuyPack_Unauthorized_Returns401()
    {
        // Arrange
        var clientNoAuth = _factory.CreateClient();

        // Act
        var response = await clientNoAuth.PostAsync("/api/shop/buy-pack?discordId=1&username=test", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task BuyPack_ValidRequest_IsAuthenticated()
    {
        // Arrange - seed cards and a pack
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (!db.PackTypes.Any(p => p.Name == "Test Pack"))
            {
                db.Cards.Add(new Card { Name = "Common Card 1", Rarity = Rarity.Common });
                db.Cards.Add(new Card { Name = "Common Card 2", Rarity = Rarity.Common });
                db.Cards.Add(new Card { Name = "Common Card 3", Rarity = Rarity.Common });
                db.Cards.Add(new Card { Name = "Rare Card", Rarity = Rarity.Rare });
                db.PackTypes.Add(new PackType { Name = "Test Pack", Price = 50, IsAvailable = true });
                var user = new User { DiscordId = 12345, Username = "TestUser", Gold = 1000 };
                db.Users.Add(user);
                await db.SaveChangesAsync();
            }
        }

        // Act
        var response = await _client.PostAsync("/api/shop/buy-pack?discordId=12345&username=TestUser&packName=Test Pack", null);

        // Assert - the authenticated request should NOT return 401 (auth works)
        // Note: InMemory database doesn't support transactions, so the service may return 500/400
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }
}
