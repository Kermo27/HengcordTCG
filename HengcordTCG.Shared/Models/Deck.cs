using System.ComponentModel.DataAnnotations.Schema;

namespace HengcordTCG.Shared.Models;

/// <summary>
/// A player's constructed deck for battle.
/// Contains 1 Commander, 9 Main Deck units, and 3 Closer cards.
/// </summary>
public class Deck
{
    public int Id { get; set; }

    public int UserId { get; set; }
    [ForeignKey("UserId")]
    public User User { get; set; } = null!;

    /// <summary>Optional name for the deck.</summary>
    public string Name { get; set; } = "Default";

    /// <summary>The Commander card for this deck.</summary>
    public int CommanderId { get; set; }
    [ForeignKey("CommanderId")]
    public Card Commander { get; set; } = null!;

    /// <summary>The 9 main deck cards + 3 closer cards.</summary>
    public List<DeckCard> DeckCards { get; set; } = new();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
