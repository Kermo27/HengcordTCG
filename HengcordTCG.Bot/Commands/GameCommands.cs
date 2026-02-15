using Discord;
using Discord.Interactions;
using HengcordTCG.Bot.Game;
using HengcordTCG.Shared.Clients;
using HengcordTCG.Shared.Models;

namespace HengcordTCG.Bot.Commands;

[Group("game", "Battle system commands")]
public class GameCommands : InteractionModuleBase<SocketInteractionContext>
{
    private readonly HengcordTCGClient _client;
    private readonly GameManager _gameManager;

    public GameCommands(HengcordTCGClient client, GameManager gameManager)
    {
        _client = client;
        _gameManager = gameManager;
    }

    [Group("deck", "Manage your battle deck")]
    public class DeckCommands : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly HengcordTCGClient _client;

        public DeckCommands(HengcordTCGClient client)
        {
            _client = client;
        }

        [SlashCommand("set", "Configure your battle deck")]
        public async Task SetDeck(
            [Summary("commander", "Your Commander card name")] 
            [Autocomplete(typeof(Handlers.CardAutocompleteHandler))] string commanderName,
            [Summary("main1", "Main deck card 1")] 
            [Autocomplete(typeof(Handlers.CardAutocompleteHandler))] string main1,
            [Summary("main2", "Main deck card 2")] 
            [Autocomplete(typeof(Handlers.CardAutocompleteHandler))] string main2,
            [Summary("main3", "Main deck card 3")] 
            [Autocomplete(typeof(Handlers.CardAutocompleteHandler))] string main3,
            [Summary("main4", "Main deck card 4")] 
            [Autocomplete(typeof(Handlers.CardAutocompleteHandler))] string main4,
            [Summary("main5", "Main deck card 5")] 
            [Autocomplete(typeof(Handlers.CardAutocompleteHandler))] string main5,
            [Summary("main6", "Main deck card 6")] 
            [Autocomplete(typeof(Handlers.CardAutocompleteHandler))] string main6,
            [Summary("main7", "Main deck card 7")] 
            [Autocomplete(typeof(Handlers.CardAutocompleteHandler))] string main7,
            [Summary("main8", "Main deck card 8")] 
            [Autocomplete(typeof(Handlers.CardAutocompleteHandler))] string main8,
            [Summary("main9", "Main deck card 9")] 
            [Autocomplete(typeof(Handlers.CardAutocompleteHandler))] string main9,
            [Summary("closer1", "Closer card 1")] 
            [Autocomplete(typeof(Handlers.CardAutocompleteHandler))] string closer1,
            [Summary("closer2", "Closer card 2")] 
            [Autocomplete(typeof(Handlers.CardAutocompleteHandler))] string closer2,
            [Summary("closer3", "Closer card 3")] 
            [Autocomplete(typeof(Handlers.CardAutocompleteHandler))] string closer3)
        {
            await DeferAsync(ephemeral: true);

            // Resolve all card names to IDs
            var allCards = await _client.GetCardsAsync();
            var cardLookup = allCards.ToDictionary(c => c.Name.ToLowerInvariant(), c => c);

            var commanderCard = FindCard(cardLookup, commanderName);
            if (commanderCard == null)
            {
                await FollowupAsync($"❌ Commander card '{commanderName}' not found.", ephemeral: true);
                return;
            }

            var mainNames = new[] { main1, main2, main3, main4, main5, main6, main7, main8, main9 };
            var mainIds = new List<int>();
            foreach (var name in mainNames)
            {
                var card = FindCard(cardLookup, name);
                if (card == null)
                {
                    await FollowupAsync($"❌ Card '{name}' not found.", ephemeral: true);
                    return;
                }
                mainIds.Add(card.Id);
            }

            var closerNames = new[] { closer1, closer2, closer3 };
            var closerIds = new List<int>();
            foreach (var name in closerNames)
            {
                var card = FindCard(cardLookup, name);
                if (card == null)
                {
                    await FollowupAsync($"❌ Card '{name}' not found.", ephemeral: true);
                    return;
                }
                closerIds.Add(card.Id);
            }

            var request = new HengcordTCGClient.SaveDeckRequest(
                Context.User.Id, null, commanderCard.Id, mainIds, closerIds
            );

            var result = await _client.SaveDeckAsync(request);

            if (result.Success)
            {
                var embed = new EmbedBuilder()
                    .WithTitle("⚔️ Deck Saved!")
                    .WithDescription($"Your battle deck has been configured.")
                    .AddField("👑 Commander", commanderCard.Name, inline: true)
                    .AddField("🃏 Main Deck", string.Join(", ", mainNames), inline: false)
                    .AddField("💥 Closers", string.Join(", ", closerNames), inline: false)
                    .WithColor(Color.Gold)
                    .Build();

                await FollowupAsync(embed: embed, ephemeral: true);
            }
            else
            {
                await FollowupAsync($"❌ {result.Message}", ephemeral: true);
            }
        }

        [SlashCommand("show", "View your current battle deck")]
        public async Task ShowDeck(
            [Summary("player", "View another player's deck (optional)")] IUser? player = null)
        {
            await DeferAsync();

            var targetUser = player ?? Context.User;
            var deck = await _client.GetDeckAsync(targetUser.Id);

            if (deck == null)
            {
                await FollowupAsync($"❌ {(player == null ? "You don't" : $"{targetUser.Username} doesn't")} have a deck configured yet. Use `/game deck set` to create one.");
                return;
            }

            var mainCards = deck.DeckCards.Where(dc => dc.Slot == DeckSlot.MainDeck).Select(dc => dc.Card).ToList();
            var closerCards = deck.DeckCards.Where(dc => dc.Slot == DeckSlot.Closer).Select(dc => dc.Card).ToList();

            var embed = new EmbedBuilder()
                .WithTitle($"⚔️ {targetUser.Username}'s Battle Deck")
                .WithDescription($"**Deck Name:** {deck.Name}")
                .AddField("👑 Commander", FormatCommanderInfo(deck.Commander), inline: false)
                .AddField($"🃏 Main Deck ({mainCards.Count}/9)", FormatCardList(mainCards), inline: false)
                .AddField($"💥 Closers ({closerCards.Count}/3)", FormatCardList(closerCards), inline: false)
                .WithColor(Color.Purple)
                .WithFooter($"Last updated: {deck.UpdatedAt:yyyy-MM-dd HH:mm} UTC")
                .Build();

            await FollowupAsync(embed: embed);
        }

        private static Card? FindCard(Dictionary<string, Card> lookup, string name)
        {
            if (lookup.TryGetValue(name.ToLowerInvariant(), out var card))
                return card;
            // Try partial match
            var match = lookup.Values.FirstOrDefault(c => 
                c.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
            return match;
        }

        private static string FormatCommanderInfo(Card commander)
        {
            return $"**{commander.Name}** — ❤️ {commander.Health} HP | ⚡ {commander.Speed} SPD | ⚔️ {commander.MinDamage}-{commander.MaxDamage} DMG | 🛡️ {commander.CounterStrike} Counter\n" +
                   (commander.AbilityText != null ? $"*{commander.AbilityText}*" : "");
        }

        private static string FormatCardList(List<Card> cards)
        {
            if (cards.Count == 0) return "(empty)";
            return string.Join("\n", cards.Select(c =>
                $"• **{c.Name}** — 💡 {c.LightCost} | ⚔️ {c.MinDamage}-{c.MaxDamage} DMG | ❤️ {c.Health} HP"));
        }
    }

    [SlashCommand("challenge", "Challenge another player to a battle")]
    public async Task Challenge(
        [Summary("opponent", "Who do you want to fight?")] IUser opponent)
    {
        if (opponent.IsBot)
        {
            await RespondAsync("🤖 Bots can't be challenged... yet.", ephemeral: true);
            return;
        }

        if (opponent.Id == Context.User.Id)
        {
            await RespondAsync("🤔 You can't challenge yourself!", ephemeral: true);
            return;
        }

        // Check both players have decks
        var myDeck = await _client.GetDeckAsync(Context.User.Id);
        var theirDeck = await _client.GetDeckAsync(opponent.Id);

        if (myDeck == null)
        {
            await RespondAsync("❌ You need to set up your deck first! Use `/game deck set`.", ephemeral: true);
            return;
        }

        if (theirDeck == null)
        {
            await RespondAsync($"❌ {opponent.Username} doesn't have a deck set up yet.", ephemeral: true);
            return;
        }

        // Send challenge
        var embed = new EmbedBuilder()
            .WithTitle("⚔️ Battle Challenge!")
            .WithDescription($"**{Context.User.Username}** challenges **{opponent.Username}** to a TCG battle!")
            .AddField($"🔴 {Context.User.Username}", $"Commander: **{myDeck.Commander.Name}**", inline: true)
            .AddField($"🔵 {opponent.Username}", $"Commander: **{theirDeck.Commander.Name}**", inline: true)
            .WithColor(Color.Red)
            .Build();

        var components = new ComponentBuilder()
            .WithButton("Accept ⚔️", $"game_accept:{Context.User.Id}:{opponent.Id}", ButtonStyle.Success)
            .WithButton("Decline", $"game_decline:{Context.User.Id}:{opponent.Id}", ButtonStyle.Danger)
            .Build();

        await RespondAsync($"{opponent.Mention}, you've been challenged!", embed: embed, components: components);
    }

    [SlashCommand("stats", "View battle statistics")]
    public async Task Stats(
        [Summary("player", "View another player's stats (optional)")] IUser? player = null)
    {
        await DeferAsync();

        var targetUser = player ?? Context.User;
        var stats = await _client.GetPlayerStatsAsync(targetUser.Id);

        if (stats == null || stats.TotalGames == 0)
        {
            await FollowupAsync($"📊 **{targetUser.Username}** hasn't played any matches yet.");
            return;
        }

        var embed = new EmbedBuilder()
            .WithTitle($"📊 {targetUser.Username}'s Battle Stats")
            .AddField("🏆 Record", $"**{stats.Wins}W** / **{stats.Losses}L** ({stats.WinRate}% WR)", inline: true)
            .AddField("🎮 Total Games", stats.TotalGames.ToString(), inline: true)
            .AddField("❤️ Best Win HP", stats.BestHpRemaining.ToString(), inline: true)
            .AddField("⚡ Fastest Win", $"{stats.ShortestWin} turns", inline: true)
            .WithColor(stats.WinRate >= 50 ? Color.Gold : Color.LightGrey);

        if (stats.RecentMatches.Count > 0)
        {
            var recentStr = string.Join("\n", stats.RecentMatches.Take(5).Select(m =>
                $"{(m.Won ? "✅" : "❌")} vs **{m.OpponentName}** — {m.Turns} turns"));
            embed.AddField("📋 Recent Matches", recentStr, inline: false);
        }

        await FollowupAsync(embed: embed.Build());
    }

    [SlashCommand("leaderboard", "View the battle leaderboard")]
    public async Task Leaderboard()
    {
        await DeferAsync();

        var entries = await _client.GetLeaderboardAsync(10);

        if (entries.Count == 0)
        {
            await FollowupAsync("📊 No matches played yet!");
            return;
        }

        var lines = entries.Select((e, i) =>
        {
            var medal = i switch { 0 => "🥇", 1 => "🥈", 2 => "🥉", _ => $"`{i + 1}.`" };
            return $"{medal} **{e.Username}** — {e.Wins}W/{e.Losses}L ({e.WinRate}%WR)";
        });

        var embed = new EmbedBuilder()
            .WithTitle("🏆 Battle Leaderboard")
            .WithDescription(string.Join("\n", lines))
            .WithColor(Color.Gold)
            .Build();

        await FollowupAsync(embed: embed);
    }

    [SlashCommand("forfeit", "Surrender your current match")]
    public async Task Forfeit()
    {
        var session = _gameManager.Forfeit(Context.User.Id);
        if (session == null)
        {
            await RespondAsync("🏳️ You are not currently in a match.", ephemeral: true);
            return;
        }

        // Persist match result
        var winner = session.Winner!;
        var loser = session.GetOpponent(winner);
        await _client.SaveMatchAsync(new HengcordTCGClient.SaveMatchRequest(
            winner.DiscordId, loser.DiscordId,
            session.TurnNumber, Math.Max(0, winner.CommanderHp)
        ));

        var embed = new EmbedBuilder()
            .WithTitle("🏳️ Forfeit")
            .WithDescription($"**{Context.User.Username}** surrendered. **{winner.Username}** wins!")
            .WithColor(Color.DarkGrey)
            .Build();

        _gameManager.EndGame(session);
        await RespondAsync(embed: embed);
    }
}
