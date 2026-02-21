using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using HengcordTCG.Server.Tests.Helpers;
using HengcordTCG.Shared.Data;
using HengcordTCG.Shared.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace HengcordTCG.Server.Tests.Controllers;

public class AuthIntegrationTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;
    private readonly HttpClient _client;

    public AuthIntegrationTests(CustomWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private string GenerateToken(string discordId, string username, string role = "User")
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(CustomWebAppFactory.TestJwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, discordId),
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Role, role),
            new Claim("sub", discordId)
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
    public async Task Me_NoAuth_Returns401()
    {
        var response = await _client.GetAsync("/api/auth/me");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_WithBearerToken_ReturnsUserDetail()
    {
        // Arrange
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var existing = await db.Users.FirstOrDefaultAsync(u => u.DiscordId == 999111);
            if (existing == null)
            {
                db.Users.Add(new User { DiscordId = 999111, Username = "JwtUser", Gold = 777 });
                await db.SaveChangesAsync();
            }
        }

        var token = GenerateToken("999111", "JwtUser");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync("/api/auth/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("name").GetString().Should().Be("JwtUser");
        json.GetProperty("isAuthenticated").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Me_WithApiKey_ReturnsBotUser()
    {
        // Arrange
        var clientWithApiKey = _factory.CreateClient();
        clientWithApiKey.DefaultRequestHeaders.Add("X-API-KEY", "test-api-key");

        // Act
        var response = await clientWithApiKey.GetAsync("/api/auth/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("name").GetString().Should().Be("Bot");
        json.GetProperty("isAuthenticated").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Logout_ClearsCookie_AndRedirects()
    {
        // Use a client that does NOT follow redirects
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Act
        var response = await client.PostAsync("/api/auth/logout", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
    }
}
