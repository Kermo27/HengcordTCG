using System.Collections.Concurrent;
using Discord.WebSocket;
using HengcordTCG.Shared.Clients;
using HengcordTCG.Shared.Models;
using Microsoft.Extensions.Logging;

namespace HengcordTCG.Bot.Game;

/// <summary>
/// Manages active game sessions and challenge flow.
/// Singleton service registered in DI.
/// </summary>
public class GameManager : IDisposable
{
    /// <summary>Active games by game ID.</summary>
    private readonly ConcurrentDictionary<string, GameSession> _games = new();

    /// <summary>Map player Discord ID → active game ID. Each player can only be in one game.</summary>
    private readonly ConcurrentDictionary<ulong, string> _playerGames = new();

    private readonly HengcordTCGClient _client;
    private readonly DiscordSocketClient _discord;
    private readonly ILogger<GameManager> _logger;
    private readonly Timer _afkTimer;

    /// <summary>Max idle time before a game is auto-forfeited.</summary>
    private static readonly TimeSpan AfkTimeout = TimeSpan.FromMinutes(5);

    public GameManager(HengcordTCGClient client, DiscordSocketClient discord, ILogger<GameManager> logger)
    {
        _client = client;
        _discord = discord;
        _logger = logger;

        // Check for stale games every 60 seconds
        _afkTimer = new Timer(CheckAfkGames, null, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60));
    }

    /// <summary>Check if a player is currently in a game.</summary>
    public bool IsPlayerInGame(ulong discordId) => _playerGames.ContainsKey(discordId);

    /// <summary>Get the active game for a player.</summary>
    public GameSession? GetGameForPlayer(ulong discordId)
    {
        if (_playerGames.TryGetValue(discordId, out var gameId) && _games.TryGetValue(gameId, out var session))
            return session;
        return null;
    }

    /// <summary>Start a new game between two players.</summary>
    public async Task<(bool Success, GameSession? Session, string Message)> StartGame(
        ulong p1Id, string p1Username,
        ulong p2Id, string p2Username,
        ulong channelId)
    {
        if (IsPlayerInGame(p1Id))
            return (false, null, $"{p1Username} is already in a game");
        if (IsPlayerInGame(p2Id))
            return (false, null, $"{p2Username} is already in a game");

        // Fetch both decks
        var p1Deck = await _client.GetDeckAsync(p1Id);
        var p2Deck = await _client.GetDeckAsync(p2Id);

        if (p1Deck == null) return (false, null, $"{p1Username} doesn't have a deck configured");
        if (p2Deck == null) return (false, null, $"{p2Username} doesn't have a deck configured");

        // Create session
        var session = GameSession.Create(p1Id, p1Username, p1Deck, p2Id, p2Username, p2Deck, channelId);

        // Register
        _games[session.GameId] = session;
        _playerGames[p1Id] = session.GameId;
        _playerGames[p2Id] = session.GameId;

        // Start first turn
        GameEngine.StartTurn(session);

        _logger.LogInformation("Game {GameId} started: {P1} vs {P2}", session.GameId, p1Username, p2Username);
        return (true, session, "Game started!");
    }

    /// <summary>Record activity on a game to prevent AFK timeout.</summary>
    public void TouchGame(GameSession session)
    {
        session.LastActivity = DateTime.UtcNow;
    }

    /// <summary>End a game and clean up tracking data.</summary>
    public void EndGame(GameSession session)
    {
        _games.TryRemove(session.GameId, out _);
        _playerGames.TryRemove(session.Player1.DiscordId, out _);
        _playerGames.TryRemove(session.Player2.DiscordId, out _);
        _logger.LogInformation("Game {GameId} ended", session.GameId);
    }

    /// <summary>Forfeit: the given player loses immediately.</summary>
    public GameSession? Forfeit(ulong discordId)
    {
        var session = GetGameForPlayer(discordId);
        if (session == null) return null;

        var loser = session.GetPlayer(discordId);
        if (loser == null) return null;

        session.IsFinished = true;
        session.Winner = session.GetOpponent(loser);
        session.GameLog.Add($"  🏳️ {loser.Username} forfeited. {session.Winner.Username} wins!");

        return session;
    }

    /// <summary>Get the number of active games.</summary>
    public int ActiveGameCount => _games.Count;

    /// <summary>Check for games that have been idle too long and auto-forfeit them.</summary>
    private async void CheckAfkGames(object? state)
    {
        try
        {
            var now = DateTime.UtcNow;
            var staleGames = _games.Values
                .Where(g => !g.IsFinished && (now - g.LastActivity) > AfkTimeout)
                .ToList();

            foreach (var session in staleGames)
            {
                _logger.LogInformation("Game {GameId} timed out due to inactivity", session.GameId);

                session.IsFinished = true;
                session.GameLog.Add("  ⏰ Game timed out due to inactivity.");

                // Try to send timeout message to the channel
                try
                {
                    var channel = _discord.GetChannel(session.ChannelId) as ISocketMessageChannel;
                    if (channel != null)
                    {
                        await channel.SendMessageAsync(
                            $"⏰ The game between **{session.Player1.Username}** and **{session.Player2.Username}** " +
                            $"has been cancelled due to inactivity ({AfkTimeout.TotalMinutes} min).");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send timeout message for game {GameId}", session.GameId);
                }

                EndGame(session);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking AFK games");
        }
    }

    public void Dispose()
    {
        _afkTimer?.Dispose();
    }
}
