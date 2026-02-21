using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using HengcordTCG.Server.Tests.Helpers;
using HengcordTCG.Shared.Models;
using HengcordTCG.Shared.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace HengcordTCG.Server.Tests.Controllers;

public class AdminControllerIntegrationTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;
    private readonly HttpClient _client;

    public AdminControllerIntegrationTests(CustomWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        // Admin controller requires JWT Bearer with Admin role
        var token = GenerateAdminToken();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private string GenerateAdminToken()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(CustomWebAppFactory.TestJwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "99999"),
            new Claim(ClaimTypes.Name, "AdminBot"),
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim("sub", "99999")
        };

        var token = new JwtSecurityToken(
            issuer: CustomWebAppFactory.TestJwtIssuer,
            audience: CustomWebAppFactory.TestJwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    [Fact]
    public async Task GiveGold_ValidRequest_UpdatesBalance()
    {
        // Arrange
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            // Cleanup in case of reuse
            var existing = await db.Users.FirstOrDefaultAsync(u => u.DiscordId == 55555);
            if (existing != null) db.Users.Remove(existing);
            db.Users.Add(new User { DiscordId = 55555, Username = "RichUser", Gold = 100 });
            await db.SaveChangesAsync();
        }

        // Act
        var response = await _client.PostAsync("/api/admin/give-gold?discordId=55555&amount=500", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var updated = await db.Users.FirstAsync(u => u.DiscordId == 55555);
            updated.Gold.Should().Be(600);
        }
    }

    [Fact]
    public async Task FixInventory_RemovesDuplicates_ReturnsOk()
    {
        // Arrange
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var card = new Card { Name = "FixInvCard" };
            db.Cards.Add(card);
            await db.SaveChangesAsync();

            var user = new User { DiscordId = 66666, Username = "MessyUser" };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            // Add duplicate entries for same user/card
            db.UserCards.Add(new UserCard { UserId = user.Id, CardId = card.Id, Count = 1, ObtainedAt = DateTime.UtcNow });
            db.UserCards.Add(new UserCard { UserId = user.Id, CardId = card.Id, Count = 2, ObtainedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        // Act
        var response = await _client.PostAsync("/api/admin/fix-inventory?discordId=66666", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var counts = await db.UserCards.Where(uc => uc.User.DiscordId == 66666).ToListAsync();
            counts.Count.Should().Be(1);
            counts[0].Count.Should().Be(3);
        }
    }

    [Fact]
    public async Task TogglePack_ChangesStatus()
    {
        // Arrange
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var pack = new PackType { Name = "TogglePack", IsAvailable = true };
            db.PackTypes.Add(pack);
            await db.SaveChangesAsync();
        }

        // Act - route is toggle-pack/{packName}
        var response = await _client.PostAsync("/api/admin/toggle-pack/TogglePack", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var updated = await db.PackTypes.FirstAsync(p => p.Name == "TogglePack");
            updated.IsAvailable.Should().BeFalse();
        }
    }

    [Fact]
    public async Task SetGold_ValidRequest_SetsExactBalance()
    {
        // Arrange
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var existing = await db.Users.FirstOrDefaultAsync(u => u.DiscordId == 77771);
            if (existing != null) db.Users.Remove(existing);
            db.Users.Add(new User { DiscordId = 77771, Username = "TargetUser", Gold = 1000 });
            await db.SaveChangesAsync();
        }

        // Act
        var response = await _client.PostAsync("/api/admin/set-gold?discordId=77771&amount=250", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var updated = await db.Users.FirstAsync(u => u.DiscordId == 77771);
            updated.Gold.Should().Be(250);
        }
    }

    [Fact]
    public async Task AddAdmin_UpdatesIsBotAdmin()
    {
        // Arrange
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.Add(new User { DiscordId = 88881, Username = "FutureAdmin", IsBotAdmin = false });
            await db.SaveChangesAsync();
        }

        // Act - route is add-admin/{discordId}
        var response = await _client.PostAsync("/api/admin/add-admin/88881", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var updated = await db.Users.FirstAsync(u => u.DiscordId == 88881);
            updated.IsBotAdmin.Should().BeTrue();
        }
    }

    [Fact]
    public async Task RemoveAdmin_UpdatesIsBotAdmin()
    {
        // Arrange
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.Add(new User { DiscordId = 88882, Username = "ExAdmin", IsBotAdmin = true });
            await db.SaveChangesAsync();
        }

        // Act - route is remove-admin/{discordId}
        var response = await _client.PostAsync("/api/admin/remove-admin/88882", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var updated = await db.Users.FirstAsync(u => u.DiscordId == 88882);
            updated.IsBotAdmin.Should().BeFalse();
        }
    }
}
