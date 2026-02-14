using System.ComponentModel.DataAnnotations.Schema;

namespace HengcordTCG.Shared.Models;

/// <summary>
/// A card within a deck. Tracks which slot it occupies (MainDeck or Closer).
/// </summary>
public class DeckCard
{
    public int Id { get; set; }

    public int DeckId { get; set; }
    [ForeignKey("DeckId")]
    public Deck Deck { get; set; } = null!;

    public int CardId { get; set; }
    [ForeignKey("CardId")]
    public Card Card { get; set; } = null!;

    /// <summary>Which part of the deck this card belongs to.</summary>
    public DeckSlot Slot { get; set; }
}

public enum DeckSlot
{
    MainDeck = 0,
    Closer = 1
}
