using Discord;
using Discord.WebSocket;
using HengcordTCG.Bot.Game;
using HengcordTCG.Shared.Clients;
using Microsoft.Extensions.Logging;

namespace HengcordTCG.Bot.Handlers;

/// <summary>
/// Handles all game-related button interactions from Discord components.
/// Registered in InteractionHandler to process component interactions.
/// </summary>
public class GameButtonHandler
{
    private readonly GameManager _gameManager;
    private readonly HengcordTCGClient _client;
    private readonly ILogger<GameButtonHandler> _logger;

    public GameButtonHandler(GameManager gameManager, HengcordTCGClient client, ILogger<GameButtonHandler> logger)
    {
        _gameManager = gameManager;
        _client = client;
        _logger = logger;
    }

    /// <summary>Main entry point: route button clicks to the appropriate handler.</summary>
    public async Task HandleButtonAsync(SocketMessageComponent component)
    {
        var customId = component.Data.CustomId;
        var parts = customId.Split(':');
        var action = parts[0];

        try
        {
            switch (action)
            {
                case "game_accept":
                    await HandleAcceptChallenge(component, parts);
                    break;
                case "game_decline":
                    await HandleDeclineChallenge(component, parts);
                    break;
                case "game_play":
                    await HandlePlayCard(component, parts);
                    break;
                case "game_pass":
                    await HandlePass(component, parts);
                    break;
                case "game_assign_atk":
                    await HandleAssignAttacker(component, parts);
                    break;
                case "game_assign_def":
                    await HandleAssignDefender(component, parts);
                    break;
                case "game_declare_done":
                    await HandleDeclarationDone(component, parts);
                    break;
                case "game_hand":
                    await HandleViewHand(component, parts);
                    break;
                case "game_combat":
                    await HandleResolveCombat(component, parts);
                    break;
                case "game_next_turn":
                    await HandleNextTurn(component, parts);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling game button {CustomId}", customId);
            await component.RespondAsync("❌ An error occurred.", ephemeral: true);
        }
    }

    private async Task HandleAcceptChallenge(SocketMessageComponent component, string[] parts)
    {
        // Format: game_accept:challengerId:targetId
        var challengerId = ulong.Parse(parts[1]);
        var targetId = ulong.Parse(parts[2]);

        if (component.User.Id != targetId)
        {
            await component.RespondAsync("❌ This challenge isn't for you!", ephemeral: true);
            return;
        }

        await component.DeferAsync();

        var challenger = await component.Channel.GetUserAsync(challengerId);
        var result = await _gameManager.StartGame(
            challengerId, challenger?.Username ?? "Player 1",
            targetId, component.User.Username,
            component.Channel.Id);

        if (!result.Success)
        {
            await component.FollowupAsync($"❌ {result.Message}");
            return;
        }

        var session = result.Session!;

        // Disable the accept/decline buttons
        await component.ModifyOriginalResponseAsync(msg =>
        {
            msg.Components = new ComponentBuilder().Build(); // Clear buttons
        });

        // Send the main game embed
        var embed = GameRenderer.BuildGameEmbed(session);
        var buttons = BuildPhaseButtons(session);
        var gameMsg = await component.FollowupAsync(embed: embed, components: buttons);
        session.MessageId = gameMsg.Id;
    }

    private async Task HandleDeclineChallenge(SocketMessageComponent component, string[] parts)
    {
        var targetId = ulong.Parse(parts[2]);

        if (component.User.Id != targetId)
        {
            await component.RespondAsync("❌ This challenge isn't for you!", ephemeral: true);
            return;
        }

        await component.UpdateAsync(msg =>
        {
            msg.Embed = new EmbedBuilder()
                .WithTitle("Challenge Declined")
                .WithDescription($"{component.User.Username} declined the challenge.")
                .WithColor(Color.DarkGrey)
                .Build();
            msg.Components = new ComponentBuilder().Build();
        });
    }

    private async Task HandlePlayCard(SocketMessageComponent component, string[] parts)
    {
        // Format: game_play:gameId:handIndex:isCloser
        var session = _gameManager.GetGameForPlayer(component.User.Id);
        if (session == null) { await component.RespondAsync("❌ Not in a game.", ephemeral: true); return; }

        var player = session.GetPlayer(component.User.Id);
        if (player == null) { await component.RespondAsync("❌ Not in this game.", ephemeral: true); return; }

        var handIndex = int.Parse(parts[2]);
        var isCloser = bool.Parse(parts[3]);

        var result = GameEngine.PlayCard(session, player, handIndex, isCloser);
        if (!result.Success)
        {
            await component.RespondAsync($"❌ {result.Message}", ephemeral: true);
            return;
        }

        await UpdateGameMessage(component, session);
    }

    private async Task HandlePass(SocketMessageComponent component, string[] parts)
    {
        var session = _gameManager.GetGameForPlayer(component.User.Id);
        if (session == null) { await component.RespondAsync("❌ Not in a game.", ephemeral: true); return; }

        var player = session.GetPlayer(component.User.Id);
        if (player == null) return;

        GameEngine.Pass(session, player);
        await UpdateGameMessage(component, session);
    }

    private async Task HandleAssignAttacker(SocketMessageComponent component, string[] parts)
    {
        // Format: game_assign_atk:gameId:unitIndex:laneIndex
        var session = _gameManager.GetGameForPlayer(component.User.Id);
        if (session == null) { await component.RespondAsync("❌ Not in a game.", ephemeral: true); return; }

        var player = session.GetPlayer(component.User.Id);
        if (player == null) return;

        var unitIndex = int.Parse(parts[2]);
        var laneIndex = int.Parse(parts[3]);

        var result = GameEngine.AssignAttacker(session, player, unitIndex, laneIndex);
        if (!result.Success)
        {
            await component.RespondAsync($"❌ {result.Message}", ephemeral: true);
            return;
        }

        await UpdateGameMessage(component, session);
    }

    private async Task HandleAssignDefender(SocketMessageComponent component, string[] parts)
    {
        // Format: game_assign_def:gameId:unitIndex:laneIndex
        var session = _gameManager.GetGameForPlayer(component.User.Id);
        if (session == null) { await component.RespondAsync("❌ Not in a game.", ephemeral: true); return; }

        var player = session.GetPlayer(component.User.Id);
        if (player == null) return;

        var unitIndex = int.Parse(parts[2]);
        var laneIndex = int.Parse(parts[3]);

        var result = GameEngine.AssignDefender(session, player, unitIndex, laneIndex);
        if (!result.Success)
        {
            await component.RespondAsync($"❌ {result.Message}", ephemeral: true);
            return;
        }

        await UpdateGameMessage(component, session);
    }

    private async Task HandleDeclarationDone(SocketMessageComponent component, string[] parts)
    {
        // Format: game_declare_done:gameId:role
        var session = _gameManager.GetGameForPlayer(component.User.Id);
        if (session == null) { await component.RespondAsync("❌ Not in a game.", ephemeral: true); return; }

        var role = parts[2]; // "attacker" or "defender"
        var player = session.GetPlayer(component.User.Id);
        if (player == null) return;

        if (role == "attacker" && player == session.Attacker)
        {
            // Attacker done assigning — now defender blocks
            await UpdateGameMessage(component, session);
        }
        else if (role == "defender" && player == session.Defender)
        {
            // Both done — move to combat
            GameEngine.FinishDeclaration(session);
            await UpdateGameMessage(component, session);
        }
        else
        {
            await component.RespondAsync("❌ Not your turn.", ephemeral: true);
        }
    }

    private async Task HandleViewHand(SocketMessageComponent component, string[] parts)
    {
        var session = _gameManager.GetGameForPlayer(component.User.Id);
        if (session == null) { await component.RespondAsync("❌ Not in a game.", ephemeral: true); return; }

        var player = session.GetPlayer(component.User.Id);
        if (player == null) return;

        var handEmbed = GameRenderer.BuildHandEmbed(session, player);
        var buttons = BuildPhaseButtons(session, player);
        await component.RespondAsync(embed: handEmbed, components: buttons, ephemeral: true);
    }

    private async Task HandleResolveCombat(SocketMessageComponent component, string[] parts)
    {
        var session = _gameManager.GetGameForPlayer(component.User.Id);
        if (session == null) { await component.RespondAsync("❌ Not in a game.", ephemeral: true); return; }

        GameEngine.ResolveCombat(session);
        await UpdateGameMessage(component, session);
    }

    private async Task HandleNextTurn(SocketMessageComponent component, string[] parts)
    {
        var session = _gameManager.GetGameForPlayer(component.User.Id);
        if (session == null) { await component.RespondAsync("❌ Not in a game.", ephemeral: true); return; }

        // Resolve end of turn
        GameEngine.ResolveEndOfTurn(session);

        if (session.IsFinished)
        {
            var gameOverEmbed = GameRenderer.BuildGameOverEmbed(session);
            await component.UpdateAsync(msg =>
            {
                msg.Embed = gameOverEmbed;
                msg.Components = new ComponentBuilder().Build();
            });

            // TODO: Persist MatchResult in Phase 6
            _gameManager.EndGame(session);
            return;
        }

        // Start next turn
        GameEngine.StartTurn(session);
        await UpdateGameMessage(component, session);
    }

    // ── Helpers ──

    private async Task UpdateGameMessage(SocketMessageComponent component, GameSession session)
    {
        var embed = GameRenderer.BuildGameEmbed(session);
        var buttons = BuildPhaseButtons(session);

        await component.UpdateAsync(msg =>
        {
            msg.Embed = embed;
            msg.Components = buttons;
        });
    }

    private MessageComponent BuildPhaseButtons(GameSession session, PlayerState? forPlayer = null)
    {
        var builder = new ComponentBuilder();

        switch (session.CurrentPhase)
        {
            case TurnPhase.Strategy:
                // Both players get "View Hand" button, current player gets play/pass options
                builder.WithButton("🃏 View Hand & Play", $"game_hand:{session.GameId}", ButtonStyle.Primary);
                builder.WithButton("Pass ⏭️", $"game_pass:{session.GameId}", ButtonStyle.Secondary);
                break;

            case TurnPhase.Declaration:
                // Attacker assigns to lanes, defender blocks
                if (session.Attacker.WaitingRoom.Count > 0 || session.Defender.WaitingRoom.Count > 0)
                {
                    builder.WithButton("🎯 Assign Units", $"game_hand:{session.GameId}", ButtonStyle.Primary);
                    builder.WithButton("Done ✅", $"game_declare_done:{session.GameId}:attacker", ButtonStyle.Success);
                }
                else
                {
                    builder.WithButton("Proceed to Combat ⚔️", $"game_declare_done:{session.GameId}:defender", ButtonStyle.Danger);
                }
                break;

            case TurnPhase.Combat:
                builder.WithButton("⚔️ Resolve Combat", $"game_combat:{session.GameId}", ButtonStyle.Danger);
                break;

            case TurnPhase.Resolution:
                builder.WithButton("▶️ Next Turn", $"game_next_turn:{session.GameId}", ButtonStyle.Success);
                break;
        }

        return builder.Build();
    }
}
