using System.ComponentModel.DataAnnotations;

namespace HengcordTCG.Shared.Models;

public class Card
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public int Attack { get; set; }
    public int Defense { get; set; }
    public Rarity Rarity { get; set; } = Rarity.Common;

    public string? ImageUrl { get; set; }
    
    public int? ExclusivePackId { get; set; }
    public PackType? ExclusivePack { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
