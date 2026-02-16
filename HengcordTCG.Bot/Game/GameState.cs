using HengcordTCG.Shared.Models;

namespace HengcordTCG.Bot.Game;

/// <summary>Phases of a single turn.</summary>
public enum TurnPhase
{
    Preparation,
    Strategy,
    Declaration,
    Combat,
    Resolution
}

/// <summary>Tracks one player's full state during battle.</summary>
public class PlayerState
{
    public ulong DiscordId { get; set; }
    public string Username { get; set; } = "";

    // Commander
    public Card Commander { get; set; } = null!;
    public int CommanderHp { get; set; }

    // Decks
    public List<Card> MainDeck { get; set; } = new();
    public List<Card> CloserDeck { get; set; } = new();
    public List<Card> Hand { get; set; } = new();
    public List<Card> DiscardPile { get; set; } = new();
    public List<UnitState> WaitingRoom { get; set; } = new();

    // Resources
    public int Light { get; set; } = 3;
    public int MaxLight { get; set; } = 5;
    public int DrawsThisTurn { get; set; }

    /// <summary>Whether this player has passed during the Strategy phase.</summary>
    public bool HasPassed { get; set; }
}

/// <summary>A unit on the board (in Waiting Room or assigned to a lane).</summary>
public class UnitState
{
    public Card Card { get; set; } = null!;
    public int CurrentHp { get; set; }
    public bool IsHeavy { get; set; }

    public UnitState(Card card, bool isHeavy = false)
    {
        Card = card;
        CurrentHp = card.Health;
        IsHeavy = isHeavy;
    }
}

/// <summary>A single combat lane.</summary>
public class Lane
{
    public int Index { get; set; }
    public UnitState? Attacker { get; set; }
    public UnitState? Defender { get; set; }
}

/// <summary>Result of a combat clash in one lane.</summary>
public class ClashResult
{
    public int LaneIndex { get; set; }
    public UnitState? Attacker { get; set; }
    public UnitState? Defender { get; set; }
    public int AttackerRoll { get; set; }
    public int DefenderRoll { get; set; }
    public int DamageDealt { get; set; }
    public string Description { get; set; } = "";
    public bool AttackerWon { get; set; }
    public bool WasUnopposed { get; set; }
    public int CounterStrikeDamage { get; set; }
}
