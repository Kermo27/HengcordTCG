using FluentAssertions;
using HengcordTCG.Shared.Services;
using HengcordTCG.Shared.Models;
using HengcordTCG.Shared.DTOs.Wiki;
using HengcordTCG.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace HengcordTCG.Tests.Services;

public class WikiProposalServiceTests
{
    private WikiProposalService CreateService(Shared.Data.AppDbContext db)
    {
        return new WikiProposalService(db);
    }

    [Fact]
    public async Task CreateProposal_NewPage_SavesCorrectly()
    {
        using var db = TestDbContextFactory.Create();
        var svc = CreateService(db);

        var proposal = await svc.CreateProposalAsync(new CreateProposalRequest
        {
            Type = ProposalType.NewPage,
            Title = "Proposed Page",
            Content = "Proposed Content"
        }, userId: 111);

        proposal.Should().NotBeNull();
        proposal.Title.Should().Be("Proposed Page");
        proposal.Status.Should().Be(ProposalStatus.Pending);
        
        var saved = await db.WikiProposals.FindAsync(proposal.Id);
        saved.Should().NotBeNull();
    }

    [Fact]
    public async Task ApproveProposal_NewPage_CreatesRealPage()
    {
        using var db = TestDbContextFactory.Create();
        var svc = CreateService(db);
        var wikiSvc = new WikiService(db);

        var proposal = await svc.CreateProposalAsync(new CreateProposalRequest
        {
            Type = ProposalType.NewPage,
            Title = "Real Page",
            Content = "Real Content",
            Slug = "real-page"
        }, userId: 111);

        var (success, _) = await svc.ApproveProposalAsync(proposal.Id, adminId: 999);

        success.Should().BeTrue();
        
        var page = await wikiSvc.GetPageBySlugAsync("real-page");
        page.Should().NotBeNull();
        page!.Content.Should().Be("Real Content");

        var updatedProposal = await db.WikiProposals.FindAsync(proposal.Id);
        updatedProposal!.Status.Should().Be(ProposalStatus.Approved);
    }

    [Fact]
    public async Task ApproveProposal_Edit_UpdatesExistingPage()
    {
        using var db = TestDbContextFactory.Create();
        var wikiSvc = new WikiService(db);
        var svc = CreateService(db);

        var (page, _) = await wikiSvc.CreatePageAsync(new CreateWikiPageRequest
        {
            Title = "Old Title",
            Content = "Old Content"
        }, 123);

        var proposal = await svc.CreateProposalAsync(new CreateProposalRequest
        {
            Type = ProposalType.Edit,
            WikiPageId = page!.Id,
            Title = "New Title",
            Content = "New Content"
        }, userId: 111);

        await svc.ApproveProposalAsync(proposal.Id, adminId: 999);

        var updatedPage = await db.WikiPages.FindAsync(page.Id);
        updatedPage!.Title.Should().Be("New Title");
        updatedPage.Content.Should().Be("New Content");
    }

    [Fact]
    public async Task RejectProposal_SetsStatusAndReason()
    {
        using var db = TestDbContextFactory.Create();
        var svc = CreateService(db);

        var proposal = await svc.CreateProposalAsync(new CreateProposalRequest
        {
            Type = ProposalType.NewPage,
            Title = "Bad Page"
        }, 111);

        var (success, _) = await svc.RejectProposalAsync(proposal.Id, 999, "Spam");

        success.Should().BeTrue();
        var updated = await db.WikiProposals.FindAsync(proposal.Id);
        updated!.Status.Should().Be(ProposalStatus.Rejected);
        updated.RejectionReason.Should().Be("Spam");
    }

    [Fact]
    public async Task GetPendingProposals_ReturnsOnlyPending()
    {
        using var db = TestDbContextFactory.Create();
        var svc = CreateService(db);

        await svc.CreateProposalAsync(new CreateProposalRequest { Title = "P1" }, 1);
        var p2 = await svc.CreateProposalAsync(new CreateProposalRequest { Title = "P2" }, 1);
        await svc.RejectProposalAsync(p2.Id, 999, "No");

        var pending = await svc.GetPendingProposalsAsync();
        pending.Should().HaveCount(1);
        pending[0].Title.Should().Be("P1");
    }
}
