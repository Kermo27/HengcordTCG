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

    /// <summary>
    /// Relative path to locally stored image file (e.g. "cards/dragon-sword.png").
    /// Served via /api/images/cards/{filename}.
    /// </summary>
    [MaxLength(255)]
    public string? ImagePath { get; set; }

    public int? ExclusivePackId { get; set; }
    public PackType? ExclusivePack { get; set; }

    public DateTime CreatedAt { get; set; }

    // ── Game / Battle Stats ──────────────────────────────

    public CardType CardType { get; set; } = CardType.Unit;

    /// <summary>Light cost to play this card (0–3 for Units, 4–7 for Closers).</summary>
    [Range(0, 7)]
    public int LightCost { get; set; }

    /// <summary>Hit points. For Commanders this is the starting HP pool.</summary>
    [Range(0, 999)]
    public int Health { get; set; }

    /// <summary>Commander-only: speed value for initiative roll.</summary>
    [Range(0, 20)]
    public int Speed { get; set; }

    /// <summary>Minimum damage dealt by this card.</summary>
    [Range(0, 20)]
    public int MinDamage { get; set; }

    /// <summary>Maximum damage dealt by this card.</summary>
    [Range(0, 20)]
    public int MaxDamage { get; set; }

    /// <summary>Commander-only: fixed damage dealt back when hit by an unopposed attack.</summary>
    [Range(0, 50)]
    public int CounterStrike { get; set; }

    /// <summary>Human-readable description of the card's special ability.</summary>
    [MaxLength(500)]
    public string? AbilityText { get; set; }

    /// <summary>Machine-readable ability key for the game engine (e.g. "heal_on_enter").</summary>
    [MaxLength(100)]
    public string? AbilityId { get; set; }

    public Card()
    {
        CreatedAt = DateTime.UtcNow;
    }
}
