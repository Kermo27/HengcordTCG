using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HengcordTCG.Shared.Data;
using HengcordTCG.Shared.Models;
using HengcordTCG.Server.Authentication;

namespace HengcordTCG.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WikiController : ControllerBase
{
    private readonly AppDbContext _context;

    public WikiController(AppDbContext context)
    {
        _context = context;
    }

    private static string GenerateSlug(string title)
    {
        return title.ToLowerInvariant()
            .Replace(" ", "-")
            .Replace("ą", "a")
            .Replace("ć", "c")
            .Replace("ę", "e")
            .Replace("ł", "l")
            .Replace("ń", "n")
            .Replace("ó", "o")
            .Replace("ś", "s")
            .Replace("ż", "z")
            .Replace("ź", "z");
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<List<WikiPageDto>>> GetPages()
    {
        var pages = await _context.WikiPages
            .OrderBy(p => p.Order)
            .Select(p => new WikiPageDto
            {
                Id = p.Id,
                Title = p.Title,
                Slug = p.Slug,
                ParentId = p.ParentId,
                Order = p.Order,
                UpdatedAt = p.UpdatedAt
            })
            .ToListAsync();
        return Ok(pages);
    }

    [HttpGet("tree")]
    [AllowAnonymous]
    public async Task<ActionResult<List<WikiPageTreeDto>>> GetTree()
    {
        var allPages = await _context.WikiPages
            .OrderBy(p => p.Order)
            .ToListAsync();

        var rootPages = allPages.Where(p => p.ParentId == null).ToList();
        
        var result = BuildTree(rootPages, allPages);
        return Ok(result);
    }

    private List<WikiPageTreeDto> BuildTree(List<WikiPage> parents, List<WikiPage> allPages)
    {
        var result = new List<WikiPageTreeDto>();
        foreach (var page in parents)
        {
            var dto = new WikiPageTreeDto
            {
                Id = page.Id,
                Title = page.Title,
                Slug = page.Slug,
                Order = page.Order,
                Children = BuildTree(allPages.Where(p => p.ParentId == page.Id).ToList(), allPages)
            };
            result.Add(dto);
        }
        return result;
    }

    [HttpGet("{slug}")]
    [AllowAnonymous]
    public async Task<ActionResult<WikiPageDetailDto>> GetPage(string slug)
    {
        var page = await _context.WikiPages
            .Include(p => p.Parent)
            .FirstOrDefaultAsync(p => p.Slug == slug);

        if (page == null)
            return NotFound(new { message = "Strona nie została znaleziona" });

        var dto = new WikiPageDetailDto
        {
            Id = page.Id,
            Title = page.Title,
            Slug = page.Slug,
            Content = page.Content,
            ParentId = page.ParentId,
            ParentTitle = page.Parent?.Title,
            ParentSlug = page.Parent?.Slug,
            Order = page.Order,
            CreatedAt = page.CreatedAt,
            UpdatedAt = page.UpdatedAt
        };

        return Ok(dto);
    }

    [HttpGet("search")]
    [AllowAnonymous]
    public async Task<ActionResult<List<WikiPageDto>>> Search([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q))
            return Ok(new List<WikiPageDto>());

        var results = await _context.WikiPages
            .Where(p => p.Title.Contains(q))
            .OrderBy(p => p.Title)
            .Take(20)
            .Select(p => new WikiPageDto
            {
                Id = p.Id,
                Title = p.Title,
                Slug = p.Slug,
                ParentId = p.ParentId,
                Order = p.Order,
                UpdatedAt = p.UpdatedAt
            })
            .ToListAsync();

        return Ok(results);
    }

    [HttpPost]
    [Authorize(AuthenticationSchemes = $"{JwtBearerDefaults.AuthenticationScheme},{ApiKeyAuthenticationOptions.DefaultScheme}", Roles = "Admin")]
    public async Task<ActionResult<WikiPageDto>> CreatePage([FromBody] CreateWikiPageRequest request)
    {
        var slug = string.IsNullOrWhiteSpace(request.Slug) 
            ? GenerateSlug(request.Title) 
            : request.Slug;

        var existing = await _context.WikiPages.FirstOrDefaultAsync(p => p.Slug == slug);
        if (existing != null)
            return BadRequest(new { message = "Strona z takim adresem już istnieje" });

        var discordId = User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(discordId) || !ulong.TryParse(discordId, out var editorId))
            return Unauthorized();

        var page = new WikiPage
        {
            Title = request.Title,
            Slug = slug,
            Content = request.Content,
            ParentId = request.ParentId,
            Order = request.Order,
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
            EditedBy = editorId,
            EditedAt = DateTime.UtcNow,
            ChangeDescription = "Utworzono stronę"
        };
        _context.WikiHistories.Add(history);

        await _context.SaveChangesAsync();

        return Ok(new WikiPageDto
        {
            Id = page.Id,
            Title = page.Title,
            Slug = page.Slug,
            ParentId = page.ParentId,
            Order = page.Order,
            UpdatedAt = page.UpdatedAt
        });
    }

    [HttpPut("{id}")]
    [Authorize(AuthenticationSchemes = $"{JwtBearerDefaults.AuthenticationScheme},{ApiKeyAuthenticationOptions.DefaultScheme}", Roles = "Admin")]
    public async Task<ActionResult<WikiPageDto>> UpdatePage(int id, [FromBody] UpdateWikiPageRequest request)
    {
        var page = await _context.WikiPages.FindAsync(id);
        if (page == null)
            return NotFound(new { message = "Strona nie została znaleziona" });

        var discordId = User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(discordId) || !ulong.TryParse(discordId, out var editorId))
            return Unauthorized();

        var history = new WikiHistory
        {
            WikiPageId = page.Id,
            Title = page.Title,
            Slug = page.Slug,
            Content = page.Content,
            ParentId = page.ParentId,
            Order = page.Order,
            EditedBy = editorId,
            EditedAt = DateTime.UtcNow,
            ChangeDescription = request.ChangeDescription ?? "Aktualizacja"
        };
        _context.WikiHistories.Add(history);

        page.Title = request.Title;
        if (!string.IsNullOrWhiteSpace(request.Slug))
            page.Slug = request.Slug;
        page.Content = request.Content;
        page.ParentId = request.ParentId;
        page.Order = request.Order;
        page.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new WikiPageDto
        {
            Id = page.Id,
            Title = page.Title,
            Slug = page.Slug,
            ParentId = page.ParentId,
            Order = page.Order,
            UpdatedAt = page.UpdatedAt
        });
    }

    [HttpDelete("{id}")]
    [Authorize(AuthenticationSchemes = $"{JwtBearerDefaults.AuthenticationScheme},{ApiKeyAuthenticationOptions.DefaultScheme}", Roles = "Admin")]
    public async Task<ActionResult> DeletePage(int id)
    {
        var page = await _context.WikiPages.FindAsync(id);
        if (page == null)
            return NotFound();

        _context.WikiPages.Remove(page);
        await _context.SaveChangesAsync();

        return Ok();
    }

    [HttpGet("{id}/history")]
    [AllowAnonymous]
    public async Task<ActionResult<List<WikiHistoryDto>>> GetPageHistory(int id)
    {
        var history = await _context.WikiHistories
            .Where(h => h.WikiPageId == id)
            .OrderByDescending(h => h.EditedAt)
            .Select(h => new WikiHistoryDto
            {
                Id = h.Id,
                Title = h.Title,
                Slug = h.Slug,
                Content = h.Content,
                EditedBy = h.EditedBy,
                EditedAt = h.EditedAt,
                ChangeDescription = h.ChangeDescription
            })
            .ToListAsync();

        return Ok(history);
    }

    [HttpPost("proposals")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ActionResult<WikiProposalDto>> CreateProposal([FromBody] CreateProposalRequest request)
    {
        var discordId = User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(discordId) || !ulong.TryParse(discordId, out var userId))
            return Unauthorized();

        var proposal = new WikiProposal
        {
            WikiPageId = request.WikiPageId,
            Type = request.Type,
            Title = request.Title,
            Slug = string.IsNullOrWhiteSpace(request.Slug) ? GenerateSlug(request.Title) : request.Slug,
            Content = request.Content,
            ParentId = request.ParentId,
            Order = request.Order,
            SubmittedBy = userId,
            Status = ProposalStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _context.WikiProposals.Add(proposal);
        await _context.SaveChangesAsync();

        return Ok(new WikiProposalDto
        {
            Id = proposal.Id,
            WikiPageId = proposal.WikiPageId,
            Type = proposal.Type,
            Title = proposal.Title,
            Status = proposal.Status,
            CreatedAt = proposal.CreatedAt
        });
    }

    [HttpGet("proposals")]
    [Authorize(AuthenticationSchemes = $"{JwtBearerDefaults.AuthenticationScheme},{ApiKeyAuthenticationOptions.DefaultScheme}", Roles = "Admin")]
    public async Task<ActionResult<List<WikiProposalListDto>>> GetProposals()
    {
        var proposals = await _context.WikiProposals
            .Include(p => p.WikiPage)
            .Where(p => p.Status == ProposalStatus.Pending)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new WikiProposalListDto
            {
                Id = p.Id,
                WikiPageId = p.WikiPageId,
                WikiPageTitle = p.WikiPage != null ? p.WikiPage.Title : null,
                Type = p.Type,
                Title = p.Title,
                Status = p.Status,
                SubmittedBy = p.SubmittedBy,
                CreatedAt = p.CreatedAt
            })
            .ToListAsync();

        return Ok(proposals);
    }

    [HttpGet("proposals/{id}")]
    [Authorize(AuthenticationSchemes = $"{JwtBearerDefaults.AuthenticationScheme},{ApiKeyAuthenticationOptions.DefaultScheme}", Roles = "Admin")]
    public async Task<ActionResult<WikiProposalDetailDto>> GetProposal(int id)
    {
        var proposal = await _context.WikiProposals
            .Include(p => p.WikiPage)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (proposal == null)
            return NotFound();

        return Ok(new WikiProposalDetailDto
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
        });
    }

    [HttpPost("proposals/{id}/approve")]
    [Authorize(AuthenticationSchemes = $"{JwtBearerDefaults.AuthenticationScheme},{ApiKeyAuthenticationOptions.DefaultScheme}", Roles = "Admin")]
    public async Task<ActionResult> ApproveProposal(int id)
    {
        var proposal = await _context.WikiProposals
            .Include(p => p.WikiPage)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (proposal == null)
            return NotFound();

        var discordId = User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(discordId) || !ulong.TryParse(discordId, out var adminId))
            return Unauthorized();

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
                    ChangeDescription = $"Utworzono stronę z propozycji #{proposal.Id}"
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
                            ChangeDescription = $"Zatwierdzono propozycję #{proposal.Id}"
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

        return Ok(new { message = "Propozycja zatwierdzona" });
    }

    [HttpPost("proposals/{id}/reject")]
    [Authorize(AuthenticationSchemes = $"{JwtBearerDefaults.AuthenticationScheme},{ApiKeyAuthenticationOptions.DefaultScheme}", Roles = "Admin")]
    public async Task<ActionResult> RejectProposal(int id, [FromBody] RejectProposalRequest request)
    {
        var proposal = await _context.WikiProposals.FindAsync(id);
        if (proposal == null)
            return NotFound();

        var discordId = User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(discordId) || !ulong.TryParse(discordId, out var adminId))
            return Unauthorized();

        proposal.Status = ProposalStatus.Rejected;
        proposal.RejectionReason = request.Reason;
        proposal.ProcessedAt = DateTime.UtcNow;
        proposal.ProcessedBy = adminId;

        await _context.SaveChangesAsync();

        return Ok(new { message = "Propozycja odrzucona" });
    }
}

public class WikiPageDto
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Slug { get; set; } = "";
    public int? ParentId { get; set; }
    public int Order { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class WikiPageTreeDto
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Slug { get; set; } = "";
    public int Order { get; set; }
    public List<WikiPageTreeDto> Children { get; set; } = new();
}

public class WikiPageDetailDto
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Slug { get; set; } = "";
    public string Content { get; set; } = "";
    public int? ParentId { get; set; }
    public string? ParentTitle { get; set; }
    public string? ParentSlug { get; set; }
    public int Order { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class WikiHistoryDto
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Slug { get; set; } = "";
    public string Content { get; set; } = "";
    public ulong EditedBy { get; set; }
    public DateTime EditedAt { get; set; }
    public string ChangeDescription { get; set; } = "";
}

public class CreateWikiPageRequest
{
    public string Title { get; set; } = "";
    public string? Slug { get; set; }
    public string Content { get; set; } = "";
    public int? ParentId { get; set; }
    public int Order { get; set; }
}

public class UpdateWikiPageRequest
{
    public string Title { get; set; } = "";
    public string? Slug { get; set; }
    public string Content { get; set; } = "";
    public int? ParentId { get; set; }
    public int Order { get; set; }
    public string? ChangeDescription { get; set; }
}

public class WikiProposalDto
{
    public int Id { get; set; }
    public int? WikiPageId { get; set; }
    public ProposalType Type { get; set; }
    public string Title { get; set; } = "";
    public ProposalStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class WikiProposalListDto
{
    public int Id { get; set; }
    public int? WikiPageId { get; set; }
    public string? WikiPageTitle { get; set; }
    public ProposalType Type { get; set; }
    public string Title { get; set; } = "";
    public ProposalStatus Status { get; set; }
    public ulong SubmittedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class WikiProposalDetailDto
{
    public int Id { get; set; }
    public int? WikiPageId { get; set; }
    public string? WikiPageTitle { get; set; }
    public ProposalType Type { get; set; }
    public string Title { get; set; } = "";
    public string Slug { get; set; } = "";
    public string Content { get; set; } = "";
    public int? ParentId { get; set; }
    public int Order { get; set; }
    public ulong SubmittedBy { get; set; }
    public ProposalStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateProposalRequest
{
    public int? WikiPageId { get; set; }
    public ProposalType Type { get; set; }
    public string Title { get; set; } = "";
    public string? Slug { get; set; }
    public string Content { get; set; } = "";
    public int? ParentId { get; set; }
    public int Order { get; set; }
}

public class RejectProposalRequest
{
    public string Reason { get; set; } = "";
}
