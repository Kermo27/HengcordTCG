using HengcordTCG.Shared.Models;

namespace HengcordTCG.Blazor.Client.DTOs;

public class WikiPageItem
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Slug { get; set; } = "";
    public int? ParentId { get; set; }
    public int Order { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class WikiPageTreeItem
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Slug { get; set; } = "";
    public int Order { get; set; }
    public List<WikiPageTreeItem> Children { get; set; } = new();
}

public class WikiPageDetail
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

public class WikiHistoryItem
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

public class WikiProposalItem
{
    public int Id { get; set; }
    public int? WikiPageId { get; set; }
    public ProposalType Type { get; set; }
    public string Title { get; set; } = "";
    public ProposalStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class WikiProposalListItem
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

public class WikiProposalDetail
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
    public ProposalType Type { get; set; } = ProposalType.Edit;
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
