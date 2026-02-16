namespace HengcordTCG.Shared.Models;

public class WikiPage
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Slug { get; set; } = "";
    public string Content { get; set; } = "";
    public int? ParentId { get; set; }
    public int Order { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    public WikiPage? Parent { get; set; }
    public List<WikiPage> Children { get; set; } = new();
}

public class WikiHistory
{
    public int Id { get; set; }
    public int WikiPageId { get; set; }
    public string Title { get; set; } = "";
    public string Slug { get; set; } = "";
    public string Content { get; set; } = "";
    public int? ParentId { get; set; }
    public int Order { get; set; }
    public ulong EditedBy { get; set; }
    public DateTime EditedAt { get; set; }
    public string ChangeDescription { get; set; } = "";
    
    public WikiPage? WikiPage { get; set; }
}

public enum ProposalType
{
    NewPage,
    Edit,
    Delete
}

public enum ProposalStatus
{
    Pending,
    Approved,
    Rejected
}

public class WikiProposal
{
    public int Id { get; set; }
    public int? WikiPageId { get; set; }
    public ProposalType Type { get; set; }
    public string Title { get; set; } = "";
    public string Slug { get; set; } = "";
    public string Content { get; set; } = "";
    public int? ParentId { get; set; }
    public int Order { get; set; }
    public ulong SubmittedBy { get; set; }
    public ProposalStatus Status { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public ulong? ProcessedBy { get; set; }
    
    public WikiPage? WikiPage { get; set; }
}
