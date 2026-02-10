using System.ComponentModel.DataAnnotations;

namespace HengcordTCG.Shared.Models;

public class PackType
{
    public int Id { get; set; }
    
    [Required]
    public string Name { get; set; } = string.Empty;
    
    public int Price { get; set; } = 100;
    public bool IsAvailable { get; set; } = true;
    
    public int ChanceCommon { get; set; } = 60;
    public int ChanceRare { get; set; } = 35;
    public int ChanceLegendary { get; set; } = 5;

    // Navigation property for exclusive cards
    public List<Card> ExclusiveCards { get; set; } = new();
}
