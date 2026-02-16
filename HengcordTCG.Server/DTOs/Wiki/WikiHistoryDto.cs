using HengcordTCG.Shared.Models;

namespace HengcordTCG.Server.DTOs.Wiki;

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
