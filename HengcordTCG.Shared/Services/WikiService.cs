using Microsoft.EntityFrameworkCore;
using HengcordTCG.Shared.Data;
using HengcordTCG.Shared.Models;
using HengcordTCG.Shared.DTOs.Wiki;

namespace HengcordTCG.Shared.Services;

public class WikiService
{
    private readonly AppDbContext _context;

    public WikiService(AppDbContext context)
    {
        _context = context;
    }

    public static string GenerateSlug(string title)
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

    public async Task<List<WikiPageDto>> GetPagesAsync()
    {
        return await _context.WikiPages
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
    }

    public async Task<List<WikiPageTreeDto>> GetTreeAsync()
    {
        var allPages = await _context.WikiPages
            .OrderBy(p => p.Order)
            .ToListAsync();

        var rootPages = allPages.Where(p => p.ParentId == null).ToList();
        
        return BuildTree(rootPages, allPages);
    }

    private static List<WikiPageTreeDto> BuildTree(List<WikiPage> parents, List<WikiPage> allPages)
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

    public async Task<WikiPageDetailDto?> GetPageBySlugAsync(string slug)
    {
        var page = await _context.WikiPages
            .Include(p => p.Parent)
            .FirstOrDefaultAsync(p => p.Slug == slug);

        if (page == null)
            return null;

        return new WikiPageDetailDto
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
    }

    public async Task<List<WikiPageDto>> SearchAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new List<WikiPageDto>();

        return await _context.WikiPages
            .Where(p => p.Title.Contains(query))
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
    }

    public async Task<(WikiPageDto? Page, string? Error)> CreatePageAsync(CreateWikiPageRequest request, ulong editorId)
    {
        var slug = string.IsNullOrWhiteSpace(request.Slug) 
            ? GenerateSlug(request.Title) 
            : request.Slug;

        var existing = await _context.WikiPages.FirstOrDefaultAsync(p => p.Slug == slug);
        if (existing != null)
            return (null, "Strona z takim adresem już istnieje");

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

        return (new WikiPageDto
        {
            Id = page.Id,
            Title = page.Title,
            Slug = page.Slug,
            ParentId = page.ParentId,
            Order = page.Order,
            UpdatedAt = page.UpdatedAt
        }, null);
    }

    public async Task<(WikiPageDto? Page, string? Error)> UpdatePageAsync(int id, UpdateWikiPageRequest request, ulong editorId)
    {
        var page = await _context.WikiPages.FindAsync(id);
        if (page == null)
            return (null, "Strona nie została znaleziona");

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

        return (new WikiPageDto
        {
            Id = page.Id,
            Title = page.Title,
            Slug = page.Slug,
            ParentId = page.ParentId,
            Order = page.Order,
            UpdatedAt = page.UpdatedAt
        }, null);
    }

    public async Task<bool> DeletePageAsync(int id)
    {
        var page = await _context.WikiPages.FindAsync(id);
        if (page == null)
            return false;

        _context.WikiPages.Remove(page);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<WikiHistoryDto>> GetPageHistoryAsync(int id)
    {
        return await _context.WikiHistories
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
    }
}
