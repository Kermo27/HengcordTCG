using Microsoft.EntityFrameworkCore;
using HengcordTCG.Shared.Data;
using HengcordTCG.Shared.Models;
using HengcordTCG.Shared.DTOs.Wiki;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;

namespace HengcordTCG.Shared.Services;

public class WikiProposalService
{
    private readonly AppDbContext _context;

    public WikiProposalService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<WikiProposalDto?> CreateProposalAsync(CreateProposalRequest request, ulong userId)
    {
        var proposal = new WikiProposal
        {
            WikiPageId = request.WikiPageId,
            Type = request.Type,
            Title = request.Title,
            Slug = string.IsNullOrWhiteSpace(request.Slug) 
                ? WikiService.GenerateSlug(request.Title) 
                : request.Slug,
            Content = request.Content,
            ParentId = request.ParentId,
            Order = request.Order,
            SubmittedBy = userId,
            Status = ProposalStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _context.WikiProposals.Add(proposal);
        await _context.SaveChangesAsync();

        return new WikiProposalDto
        {
            Id = proposal.Id,
            WikiPageId = proposal.WikiPageId,
            Type = proposal.Type,
            Title = proposal.Title,
            Status = proposal.Status,
            CreatedAt = proposal.CreatedAt
        };
    }

    public async Task<List<WikiProposalListDto>> GetPendingProposalsAsync()
    {
        var proposals = await _context.WikiProposals
            .Include(p => p.WikiPage)
            .Where(p => p.Status == ProposalStatus.Pending)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
        
        var userIds = proposals.Select(p => p.SubmittedBy).Distinct().ToList();
        var users = await _context.Users.Where(u => userIds.Contains(u.DiscordId)).ToDictionaryAsync(u => u.DiscordId, u => u.Username);
        
        var wikiPageIds = proposals
            .Where(p => p.Type == ProposalType.Edit && p.WikiPageId.HasValue)
            .Select(p => p.WikiPageId!.Value)
            .Distinct()
            .ToList();
        var wikiPages = await _context.WikiPages
            .Where(p => wikiPageIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Content);
        
        var result = new List<WikiProposalListDto>();
        foreach (var p in proposals)
        {
            var username = users.GetValueOrDefault(p.SubmittedBy);
            if (string.IsNullOrEmpty(username))
            {
                username = $"User_{p.SubmittedBy}";
            }
            
            var dto = new WikiProposalListDto
            {
                Id = p.Id,
                WikiPageId = p.WikiPageId,
                WikiPageTitle = p.WikiPage?.Title,
                Type = p.Type,
                Title = p.Title,
                Status = p.Status,
                SubmittedBy = p.SubmittedBy,
                SubmittedByUsername = username,
                CreatedAt = p.CreatedAt,
                Content = p.Content,
                ChangeDescription = p.Type == ProposalType.Edit ? $"Edit: {p.WikiPage?.Title}" : 
                                   p.Type == ProposalType.Delete ? $"Delete: {p.WikiPage?.Title}" : 
                                   "New page proposal"
            };
            
            if (p.Type == ProposalType.Edit && p.WikiPageId.HasValue)
            {
                var originalContent = wikiPages.GetValueOrDefault(p.WikiPageId.Value) ?? "";
                dto.OriginalContent = originalContent;
                dto.Diff = ComputeSideBySideDiff(originalContent, p.Content);
            }
            
            result.Add(dto);
        }
        
        return result;
    }
    
    private DTOs.Wiki.SideBySideDiffModel ComputeSideBySideDiff(string oldText, string newText)
    {
        var diff = SideBySideDiffBuilder.Diff(oldText, newText);
        
        return new DTOs.Wiki.SideBySideDiffModel
        {
            Left = new DiffPanel
            {
                Title = "Original",
                Lines = diff.OldText.Lines.Select(l => new DiffLine
                {
                    LineNumber = l.Position ?? 0,
                    Content = l.Text,
                    Type = l.Type.ToString().ToLower()
                }).ToList()
            },
            Right = new DiffPanel
            {
                Title = "Proposed",
                Lines = diff.NewText.Lines.Select(l => new DiffLine
                {
                    LineNumber = l.Position ?? 0,
                    Content = l.Text,
                    Type = l.Type.ToString().ToLower()
                }).ToList()
            }
        };
    }

    public async Task<WikiProposalDetailDto?> GetProposalAsync(int id)
    {
        var proposal = await _context.WikiProposals
            .Include(p => p.WikiPage)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (proposal == null)
            return null;

        return new WikiProposalDetailDto
        {
            Id = proposal.Id,
            WikiPageId = proposal.WikiPageId,
            WikiPageTitle = proposal.WikiPage?.Title,
            Type = proposal.Type,
            Title = proposal.Title,
            Slug = proposal.Slug,
            Content = proposal.Content,
            ParentId = proposal.ParentId,
            Order = proposal.Order,
            SubmittedBy = proposal.SubmittedBy,
            Status = proposal.Status,
            CreatedAt = proposal.CreatedAt
        };
    }

    public async Task<(bool Success, string? Error)> ApproveProposalAsync(int id, ulong adminId)
    {
        var proposal = await _context.WikiProposals
            .Include(p => p.WikiPage)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (proposal == null)
            return (false, "Proposal not found");

        WikiPage? page = null;

        switch (proposal.Type)
        {
            case ProposalType.NewPage:
                page = new WikiPage
                {
                    Title = proposal.Title,
                    Slug = proposal.Slug,
                    Content = proposal.Content,
                    ParentId = proposal.ParentId,
                    Order = proposal.Order,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.WikiPages.Add(page);
                await _context.SaveChangesAsync();
                
                var history = new WikiHistory
                {
                    WikiPageId = page.Id,
                    Title = page.Title,
                    Slug = page.Slug,
                    Content = page.Content,
                    ParentId = page.ParentId,
                    Order = page.Order,
                    EditedBy = proposal.SubmittedBy,
                    EditedAt = DateTime.UtcNow,
                    ChangeDescription = $"Created page from proposal #{proposal.Id}"
                };
                _context.WikiHistories.Add(history);
                await _context.SaveChangesAsync();
                break;

            case ProposalType.Edit:
                if (proposal.WikiPageId.HasValue)
                {
                    page = await _context.WikiPages.FindAsync(proposal.WikiPageId.Value);
                    if (page != null)
                    {
                        var editHistory = new WikiHistory
                        {
                            WikiPageId = page.Id,
                            Title = page.Title,
                            Slug = page.Slug,
                            Content = page.Content,
                            ParentId = page.ParentId,
                            Order = page.Order,
                            EditedBy = proposal.SubmittedBy,
                            EditedAt = DateTime.UtcNow,
                            ChangeDescription = $"Approved proposal #{proposal.Id}"
                        };
                        _context.WikiHistories.Add(editHistory);

                        page.Title = proposal.Title;
                        page.Slug = proposal.Slug;
                        page.Content = proposal.Content;
                        page.ParentId = proposal.ParentId;
                        page.Order = proposal.Order;
                        page.UpdatedAt = DateTime.UtcNow;
                    }
                }
                break;

            case ProposalType.Delete:
                if (proposal.WikiPageId.HasValue)
                {
                    page = await _context.WikiPages.FindAsync(proposal.WikiPageId.Value);
                    if (page != null)
                    {
                        var childPages = await _context.WikiPages.Where(p => p.ParentId == page.Id).ToListAsync();
                        foreach (var child in childPages)
                        {
                            child.ParentId = null;
                        }
                        
                        var histories = await _context.WikiHistories.Where(h => h.WikiPageId == page.Id).ToListAsync();
                        _context.WikiHistories.RemoveRange(histories);
                        
                        var relatedProposals = await _context.WikiProposals.Where(p => p.WikiPageId == page.Id).ToListAsync();
                        foreach (var prop in relatedProposals)
                        {
                            prop.WikiPageId = null;
                        }
                        
                        _context.WikiPages.Remove(page);
                    }
                }
                break;
        }

        proposal.Status = ProposalStatus.Approved;
        proposal.ProcessedAt = DateTime.UtcNow;
        proposal.ProcessedBy = adminId;

        await _context.SaveChangesAsync();

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> RejectProposalAsync(int id, ulong adminId, string reason)
    {
        var proposal = await _context.WikiProposals.FindAsync(id);
        if (proposal == null)
            return (false, "Proposal not found");

        proposal.Status = ProposalStatus.Rejected;
        proposal.RejectionReason = reason;
        proposal.ProcessedAt = DateTime.UtcNow;
        proposal.ProcessedBy = adminId;

        await _context.SaveChangesAsync();

        return (true, null);
    }
}
