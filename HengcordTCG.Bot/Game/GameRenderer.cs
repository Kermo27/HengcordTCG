using Discord;

namespace HengcordTCG.Bot.Game;

/// <summary>
/// Builds Discord Embeds and Component buttons for each game phase.
/// </summary>
public static class GameRenderer
{
    public static Embed BuildGameEmbed(GameSession session)
    {
        var embed = new EmbedBuilder()
            .WithTitle($"⚔️ {session.Player1.Username} vs {session.Player2.Username}")
            .WithDescription(GetPhaseDescription(session))
            .WithColor(GetPhaseColor(session.CurrentPhase))
            .WithFooter($"Turn {session.TurnNumber} | Game {session.GameId}");

        // Commander HP
        embed.AddField(
            $"🔴 {session.Player1.Username}",
            FormatPlayerStatus(session.Player1, session.IsPlayer1Attacker),
            inline: true);
        embed.AddField(
            $"🔵 {session.Player2.Username}",
            FormatPlayerStatus(session.Player2, !session.IsPlayer1Attacker),
            inline: true);

        // Phase-specific content
        switch (session.CurrentPhase)
        {
            case TurnPhase.Strategy:
                embed.AddField("📋 Phase", "**Strategy** — Play cards or pass", inline: false);
                embed.AddField($"🎯 Current turn", session.CurrentStrategyPlayer.Username, inline: true);
                break;

            case TurnPhase.Declaration:
                embed.AddField("📋 Phase", "**Declaration** — Assign units to lanes", inline: false);
                AddLaneDisplay(embed, session);
                AddWaitingRoomDisplay(embed, session);
                break;

            case TurnPhase.Combat:
            case TurnPhase.Resolution:
                AddCombatResults(embed, session);
                break;
        }

        return embed.Build();
    }

    /// <summary>Build the embed showing a specific player's hand (sent as ephemeral).</summary>
    public static Embed BuildHandEmbed(GameSession session, PlayerState player)
    {
        var embed = new EmbedBuilder()
            .WithTitle($"🃏 Your Hand")
            .WithDescription($"💡 Light: **{player.Light}/{player.MaxLight}** | Cards in deck: {player.MainDeck.Count}")
            .WithColor(Color.DarkBlue);

        if (player.Hand.Count > 0)
        {
            var handStr = string.Join("\n", player.Hand.Select((c, i) =>
                $"`{i + 1}` **{c.Name}** — 💡{c.LightCost} | ⚔️{c.MinDamage}-{c.MaxDamage} | ❤️{c.Health}"));
            embed.AddField($"Main Deck ({player.Hand.Count})", handStr);
        }

        if (player.CloserDeck.Count > 0)
        {
            var closerStr = string.Join("\n", player.CloserDeck.Select((c, i) =>
                $"`C{i + 1}` **{c.Name}** — 💡{c.LightCost} | ⚔️{c.MinDamage}-{c.MaxDamage} | ❤️{c.Health}"));
            embed.AddField($"💥 Closers ({player.CloserDeck.Count})", closerStr);
        }

        if (player.WaitingRoom.Count > 0)
        {
            var waitStr = string.Join("\n", player.WaitingRoom.Select(u =>
                $"• **{u.Card.Name}** — ❤️{u.CurrentHp}/{u.Card.Health}"));
            embed.AddField("🏠 Waiting Room", waitStr);
        }

        return embed.Build();
    }

    /// <summary>Build buttons for the Strategy phase.</summary>
    public static MessageComponent BuildStrategyButtons(GameSession session, PlayerState player)
    {
        var builder = new ComponentBuilder();

        // Card play buttons (up to 5 per row, max 25 buttons per message)
        for (int i = 0; i < Math.Min(player.Hand.Count, 9); i++)
        {
            var card = player.Hand[i];
            builder.WithButton(
                $"{card.Name} (💡{card.LightCost})",
                $"game_play:{session.GameId}:{i}:false",
                ButtonStyle.Primary,
                row: i / 5,
                disabled: player.Light < card.LightCost);
        }

        // Closer buttons
        int closerRow = (player.Hand.Count + 4) / 5;
        for (int i = 0; i < player.CloserDeck.Count; i++)
        {
            var card = player.CloserDeck[i];
            builder.WithButton(
                $"💥 {card.Name} (💡{card.LightCost})",
                $"game_play:{session.GameId}:{i}:true",
                ButtonStyle.Danger,
                row: Math.Min(closerRow, 4),
                disabled: player.Light < card.LightCost);
        }

        // Pass button
        builder.WithButton("Pass ⏭️", $"game_pass:{session.GameId}", ButtonStyle.Secondary, row: Math.Min(closerRow + 1, 4));

        return builder.Build();
    }

    /// <summary>Build buttons for the Declaration phase (attacker assigns lanes).</summary>
    public static MessageComponent BuildAttackerDeclarationButtons(GameSession session, PlayerState player)
    {
        var builder = new ComponentBuilder();

        for (int i = 0; i < player.WaitingRoom.Count; i++)
        {
            var unit = player.WaitingRoom[i];
            for (int lane = 0; lane < 3; lane++)
            {
                if (session.Lanes[lane].Attacker == null)
                {
                    builder.WithButton(
                        $"{unit.Card.Name} → Lane {lane + 1}",
                        $"game_assign_atk:{session.GameId}:{i}:{lane}",
                        ButtonStyle.Primary,
                        row: Math.Min(i, 4));
                }
            }
        }

        builder.WithButton("Done ✅", $"game_declare_done:{session.GameId}:attacker", ButtonStyle.Success, row: 4);
        return builder.Build();
    }

    /// <summary>Build buttons for the Declaration phase (defender blocks lanes).</summary>
    public static MessageComponent BuildDefenderDeclarationButtons(GameSession session, PlayerState player)
    {
        var builder = new ComponentBuilder();

        for (int i = 0; i < player.WaitingRoom.Count; i++)
        {
            var unit = player.WaitingRoom[i];
            for (int lane = 0; lane < 3; lane++)
            {
                if (session.Lanes[lane].Attacker != null && session.Lanes[lane].Defender == null)
                {
                    builder.WithButton(
                        $"{unit.Card.Name} → Block Lane {lane + 1}",
                        $"game_assign_def:{session.GameId}:{i}:{lane}",
                        ButtonStyle.Primary,
                        row: Math.Min(i, 4));
                }
            }
        }

        builder.WithButton("Done ✅", $"game_declare_done:{session.GameId}:defender", ButtonStyle.Success, row: 4);
        return builder.Build();
    }

    /// <summary>Build the game over embed.</summary>
    public static Embed BuildGameOverEmbed(GameSession session)
    {
        var winner = session.Winner!;
        var loser = session.GetOpponent(winner);

        return new EmbedBuilder()
            .WithTitle("🏆 Game Over!")
            .WithDescription($"**{winner.Username}** defeats **{loser.Username}**!")
            .AddField($"🥇 {winner.Username}", $"❤️ {winner.CommanderHp} HP remaining", inline: true)
            .AddField($"💀 {loser.Username}", $"❤️ {loser.CommanderHp} HP", inline: true)
            .AddField("📊 Stats", $"Turns: {session.TurnNumber}", inline: false)
            .WithColor(Color.Gold)
            .Build();
    }

    // ── Helpers ──

    private static string GetPhaseDescription(GameSession session)
    {
        return session.CurrentPhase switch
        {
            TurnPhase.Preparation => "🔄 Drawing cards...",
            TurnPhase.Strategy => $"⚡ {session.CurrentStrategyPlayer.Username}'s turn to play or pass",
            TurnPhase.Declaration => $"🎯 {session.Attacker.Username} assigns attackers, {session.Defender.Username} blocks",
            TurnPhase.Combat => "⚔️ Clash in progress!",
            TurnPhase.Resolution => "📋 Resolving end of turn...",
            _ => ""
        };
    }

    private static Color GetPhaseColor(TurnPhase phase)
    {
        return phase switch
        {
            TurnPhase.Strategy => Color.Blue,
            TurnPhase.Declaration => Color.Orange,
            TurnPhase.Combat => Color.Red,
            TurnPhase.Resolution => Color.Green,
            _ => Color.LightGrey
        };
    }

    private static string FormatPlayerStatus(PlayerState player, bool isAttacker)
    {
        var role = isAttacker ? "⚔️ ATK" : "🛡️ DEF";
        return $"{role}\n❤️ {player.CommanderHp} HP\n💡 {player.Light}/{player.MaxLight} Light\n" +
               $"🃏 Hand: {player.Hand.Count} | Deck: {player.MainDeck.Count}\n" +
               $"🏠 Units: {player.WaitingRoom.Count}";
    }

    private static void AddLaneDisplay(EmbedBuilder embed, GameSession session)
    {
        var laneStrs = new List<string>();
        for (int i = 0; i < 3; i++)
        {
            var lane = session.Lanes[i];
            var atk = lane.Attacker != null ? $"⚔️ {lane.Attacker.Card.Name}" : "—";
            var def = lane.Defender != null ? $"🛡️ {lane.Defender.Card.Name}" : "—";
            laneStrs.Add($"**Lane {i + 1}**: {atk} vs {def}");
        }
        embed.AddField("🏟️ Lanes", string.Join("\n", laneStrs), inline: false);
    }

    private static void AddWaitingRoomDisplay(EmbedBuilder embed, GameSession session)
    {
        foreach (var player in new[] { session.Attacker, session.Defender })
        {
            if (player.WaitingRoom.Count > 0)
            {
                var units = string.Join(", ", player.WaitingRoom.Select(u => u.Card.Name));
                embed.AddField($"🏠 {player.Username}'s Waiting Room", units, inline: true);
            }
        }
    }

    private static void AddCombatResults(EmbedBuilder embed, GameSession session)
    {
        if (session.LastCombatResults.Count > 0)
        {
            var resultStr = string.Join("\n", session.LastCombatResults.Select(r => r.Description));
            embed.AddField("⚔️ Combat Results", resultStr, inline: false);
        }
        else
        {
            embed.AddField("⚔️ Combat", "No attacks this turn", inline: false);
        }
    }
}
