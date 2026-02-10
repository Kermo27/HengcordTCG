using System.ComponentModel.DataAnnotations;

namespace HengcordTCG.Shared.Models;

public class Card
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Range(0, 100, ErrorMessage = "Attack must be between 0 and 100")]
    public int Attack { get; set; }

    [Range(0, 100, ErrorMessage = "Defense must be between 0 and 100")]
    public int Defense { get; set; }

    public Rarity Rarity { get; set; } = Rarity.Common;

    [Url(ErrorMessage = "ImageUrl must be a valid URL")]
    public string? ImageUrl { get; set; }
    
    public int? ExclusivePackId { get; set; }
    public PackType? ExclusivePack { get; set; }
    
    public DateTime CreatedAt { get; set; }

    public Card()
    {
        CreatedAt = DateTime.UtcNow;
    }
}
