using HengcordTCG.Shared.Models;

namespace HengcordTCG.Shared.DTOs.Wiki;

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
