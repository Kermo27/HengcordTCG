using FluentAssertions;
using HengcordTCG.Shared.Services;
using HengcordTCG.Shared.DTOs.Wiki;
using HengcordTCG.Tests.Helpers;

namespace HengcordTCG.Tests.Services;

public class WikiServiceTests
{
    private WikiService CreateService(Shared.Data.AppDbContext db)
    {
        return new WikiService(db);
    }

    [Theory]
    [InlineData("Żółta Kartka", "zolta-kartka")]
    [InlineData("Łódź Wielka", "lodz-wielka")]
    [InlineData("Ćma Śliczna", "cma-sliczna")]
    [InlineData("Simple Title", "simple-title")]
    public void GenerateSlug_PolishChars_Replaced(string input, string expected)
    {
        WikiService.GenerateSlug(input).Should().Be(expected);
    }

    [Fact]
    public async Task CreatePage_Success_WithHistory()
    {
        using var db = TestDbContextFactory.Create();
        var svc = CreateService(db);

        var (page, error) = await svc.CreatePageAsync(new CreateWikiPageRequest
        {
            Title = "Test Page",
            Content = "# Hello World",
            Order = 1
        }, editorId: 123);

        error.Should().BeNull();
        page.Should().NotBeNull();
        page!.Title.Should().Be("Test Page");
        page.Slug.Should().Be("test-page");

        var history = await svc.GetPageHistoryAsync(page.Id);
        history.Should().HaveCount(1);
        history[0].ChangeDescription.Should().Contain("Created");
    }

    [Fact]
    public async Task CreatePage_DuplicateSlug_Error()
    {
        using var db = TestDbContextFactory.Create();
        var svc = CreateService(db);

        await svc.CreatePageAsync(new CreateWikiPageRequest
        {
            Title = "Unique Page",
            Content = "Content"
        }, editorId: 123);

        var (page, error) = await svc.CreatePageAsync(new CreateWikiPageRequest
        {
            Title = "Unique Page",
            Content = "Different Content"
        }, editorId: 123);

        page.Should().BeNull();
        error.Should().Contain("already exists");
    }

    [Fact]
    public async Task CreatePage_CustomSlug_UsesIt()
    {
        using var db = TestDbContextFactory.Create();
        var svc = CreateService(db);

        var (page, _) = await svc.CreatePageAsync(new CreateWikiPageRequest
        {
            Title = "My Page",
            Slug = "custom-slug",
            Content = "Content"
        }, editorId: 123);

        page!.Slug.Should().Be("custom-slug");
    }

    [Fact]
    public async Task UpdatePage_Success_SavesOldHistory()
    {
        using var db = TestDbContextFactory.Create();
        var svc = CreateService(db);

        var (page, _) = await svc.CreatePageAsync(new CreateWikiPageRequest
        {
            Title = "Original",
            Content = "Original Content"
        }, editorId: 123);

        var (updated, error) = await svc.UpdatePageAsync(page!.Id, new UpdateWikiPageRequest
        {
            Title = "Updated",
            Content = "New Content",
            ChangeDescription = "Updated title"
        }, editorId: 456);

        error.Should().BeNull();
        updated!.Title.Should().Be("Updated");

        var history = await svc.GetPageHistoryAsync(page.Id);
        history.Should().HaveCount(2);
        // Most recent first — the history entry has the OLD content
        history[0].Title.Should().Be("Original");
    }

    [Fact]
    public async Task DeletePage_Exists_ReturnsTrue()
    {
        using var db = TestDbContextFactory.Create();
        var svc = CreateService(db);

        var (page, _) = await svc.CreatePageAsync(new CreateWikiPageRequest
        {
            Title = "To Delete",
            Content = "Delete me"
        }, editorId: 123);

        var result = await svc.DeletePageAsync(page!.Id);
        result.Should().BeTrue();

        var check = await svc.GetPageBySlugAsync("to-delete");
        check.Should().BeNull();
    }

    [Fact]
    public async Task DeletePage_NotExists_ReturnsFalse()
    {
        using var db = TestDbContextFactory.Create();
        var svc = CreateService(db);

        var result = await svc.DeletePageAsync(9999);
        result.Should().BeFalse();
    }

    [Fact]
    public async Task Search_MatchesTitle()
    {
        using var db = TestDbContextFactory.Create();
        var svc = CreateService(db);

        await svc.CreatePageAsync(new CreateWikiPageRequest { Title = "Dragon Guide", Content = "About dragons" }, 123);
        await svc.CreatePageAsync(new CreateWikiPageRequest { Title = "Card Rules", Content = "Game rules" }, 123);
        await svc.CreatePageAsync(new CreateWikiPageRequest { Title = "Dragon Types", Content = "Fire, ice" }, 123);

        var results = await svc.SearchAsync("Dragon");

        results.Should().HaveCount(2);
        results.Should().AllSatisfy(r => r.Title.Should().Contain("Dragon"));
    }

    [Fact]
    public async Task GetTree_NestedPages_BuildsHierarchy()
    {
        using var db = TestDbContextFactory.Create();
        var svc = CreateService(db);

        var (parent, _) = await svc.CreatePageAsync(new CreateWikiPageRequest
        {
            Title = "Parent",
            Content = "Parent content",
            Order = 1
        }, 123);

        await svc.CreatePageAsync(new CreateWikiPageRequest
        {
            Title = "Child",
            Content = "Child content",
            ParentId = parent!.Id,
            Order = 1
        }, 123);

        var tree = await svc.GetTreeAsync();

        tree.Should().HaveCount(1);
        tree[0].Title.Should().Be("Parent");
        tree[0].Children.Should().HaveCount(1);
        tree[0].Children[0].Title.Should().Be("Child");
    }

    [Fact]
    public async Task GetPageBySlug_IncludesParent()
    {
        using var db = TestDbContextFactory.Create();
        var svc = CreateService(db);

        var (parent, _) = await svc.CreatePageAsync(new CreateWikiPageRequest
        {
            Title = "Root Page",
            Content = "Root"
        }, 123);

        await svc.CreatePageAsync(new CreateWikiPageRequest
        {
            Title = "Sub Page",
            Content = "Sub",
            ParentId = parent!.Id
        }, 123);

        var detail = await svc.GetPageBySlugAsync("sub-page");

        detail.Should().NotBeNull();
        detail!.ParentTitle.Should().Be("Root Page");
        detail.ParentSlug.Should().Be("root-page");
    }

    [Fact]
    public async Task GetPages_ReturnsAll()
    {
        using var db = TestDbContextFactory.Create();
        var svc = CreateService(db);

        await svc.CreatePageAsync(new CreateWikiPageRequest { Title = "Page 1", Content = "A" }, 1);
        await svc.CreatePageAsync(new CreateWikiPageRequest { Title = "Page 2", Content = "B" }, 1);

        var pages = await svc.GetPagesAsync();
        pages.Should().HaveCount(2);
    }
}
