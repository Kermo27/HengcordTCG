using HengcordTCG.Shared.Models;

namespace HengcordTCG.Server.DTOs.Wiki;

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
