using HengcordTCG.Shared.Models;

namespace HengcordTCG.Bot.Game;

/// <summary>
/// Full mutable state for one active game between two players.
/// Stored in-memory via GameManager's ConcurrentDictionary.
/// </summary>
public class GameSession
{
    public string GameId { get; set; } = Guid.NewGuid().ToString("N")[..12];

    // Players
    public PlayerState Player1 { get; set; } = null!;
    public PlayerState Player2 { get; set; } = null!;

    /// <summary>Who currently has the Attacker role.</summary>
    public PlayerState Attacker => IsPlayer1Attacker ? Player1 : Player2;
    public PlayerState Defender => IsPlayer1Attacker ? Player2 : Player1;
    public bool IsPlayer1Attacker { get; set; }

    // Turn tracking
    public int TurnNumber { get; set; } = 0;
    public TurnPhase CurrentPhase { get; set; } = TurnPhase.Preparation;

    /// <summary>Whose turn it is to play a card during Strategy phase (alternating).</summary>
    public bool IsPlayer1Turn { get; set; }

    // Board
    public Lane[] Lanes { get; set; } = { new() { Index = 0 }, new() { Index = 1 }, new() { Index = 2 } };

    // Discord context
    public ulong ChannelId { get; set; }
    public ulong? MessageId { get; set; }

    // Game state
    public bool IsFinished { get; set; }
    public PlayerState? Winner { get; set; }
    public List<string> GameLog { get; set; } = new();
    public List<ClashResult> LastCombatResults { get; set; } = new();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastActivity { get; set; } = DateTime.UtcNow;

    /// <summary>Get a player state by Discord ID.</summary>
    public PlayerState? GetPlayer(ulong discordId)
    {
        if (Player1.DiscordId == discordId) return Player1;
        if (Player2.DiscordId == discordId) return Player2;
        return null;
    }

    /// <summary>Get the opponent of a given player.</summary>
    public PlayerState GetOpponent(PlayerState player)
    {
        return player == Player1 ? Player2 : Player1;
    }

    /// <summary>Get the current player who should act during Strategy phase.</summary>
    public PlayerState CurrentStrategyPlayer => IsPlayer1Turn ? Player1 : Player2;

    /// <summary>Initialize a game from two decks.</summary>
    public static GameSession Create(
        ulong p1DiscordId, string p1Username, Deck p1Deck,
        ulong p2DiscordId, string p2Username, Deck p2Deck,
        ulong channelId)
    {
        var session = new GameSession
        {
            ChannelId = channelId,
            Player1 = CreatePlayerState(p1DiscordId, p1Username, p1Deck),
            Player2 = CreatePlayerState(p2DiscordId, p2Username, p2Deck),
        };

        // Determine initial attacker by Commander Speed
        if (session.Player1.Commander.Speed > session.Player2.Commander.Speed)
            session.IsPlayer1Attacker = true;
        else if (session.Player2.Commander.Speed > session.Player1.Commander.Speed)
            session.IsPlayer1Attacker = false;
        else
            session.IsPlayer1Attacker = Random.Shared.Next(2) == 0; // Coin flip tie-breaker

        // Faster player acts first in strategy
        session.IsPlayer1Turn = session.IsPlayer1Attacker;

        return session;
    }

    private static PlayerState CreatePlayerState(ulong discordId, string username, Deck deck)
    {
        var mainDeck = deck.DeckCards
            .Where(dc => dc.Slot == DeckSlot.MainDeck)
            .Select(dc => dc.Card)
            .ToList();

        var closerDeck = deck.DeckCards
            .Where(dc => dc.Slot == DeckSlot.Closer)
            .Select(dc => dc.Card)
            .ToList();

        // Shuffle main deck
        Shuffle(mainDeck);

        return new PlayerState
        {
            DiscordId = discordId,
            Username = username,
            Commander = deck.Commander,
            CommanderHp = deck.Commander.Health,
            MainDeck = mainDeck,
            CloserDeck = closerDeck,
            Light = 3,
            MaxLight = 5,
        };
    }

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Shared.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
