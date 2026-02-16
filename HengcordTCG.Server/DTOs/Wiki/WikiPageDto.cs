namespace HengcordTCG.Server.DTOs.Wiki;

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
