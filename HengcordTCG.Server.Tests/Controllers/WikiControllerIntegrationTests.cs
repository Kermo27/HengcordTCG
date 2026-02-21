using System.Net;
using System.Net.Http.Json;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using HengcordTCG.Server.Tests.Helpers;
using HengcordTCG.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using HengcordTCG.Shared.Data;
using HengcordTCG.Shared.DTOs.Wiki;

namespace HengcordTCG.Server.Tests.Controllers;

public class WikiControllerIntegrationTests : IClassFixture<CustomWebAppFactory>
{
    private readonly CustomWebAppFactory _factory;
    private readonly HttpClient _client;

    public WikiControllerIntegrationTests(CustomWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetPages_ReturnsOpenPages()
    {
        // Arrange
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (!db.WikiPages.Any())
            {
                db.WikiPages.Add(new WikiPage { Title = "Home", Slug = "home", Content = "Welcome" });
                await db.SaveChangesAsync();
            }
        }

        // Act
        var response = await _client.GetAsync("/api/wiki");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var pages = await response.Content.ReadFromJsonAsync<List<WikiPageDto>>();
        pages.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetTree_ReturnsHierarchy()
    {
        // Act
        var response = await _client.GetAsync("/api/wiki/tree");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var tree = await response.Content.ReadFromJsonAsync<List<WikiPageTreeDto>>();
        tree.Should().NotBeNull();
    }

    [Fact]
    public async Task CreatePage_WithoutAuth_Returns401()
    {
        // Act
        var response = await _client.PostAsJsonAsync("/api/wiki", new CreateWikiPageRequest { Title = "Forbidden" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
