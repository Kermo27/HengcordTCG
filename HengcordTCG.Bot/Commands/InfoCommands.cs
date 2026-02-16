using Discord;
using Discord.Interactions;
using HengcordTCG.Bot.Handlers;
using HengcordTCG.Shared.Clients;

namespace HengcordTCG.Bot.Commands;

public class InfoCommands : InteractionModuleBase<SocketInteractionContext>
{
    private readonly HengcordTCGClient _client;

    public InfoCommands(HengcordTCGClient client)
    {
        _client = client;
    }

    [SlashCommand("card", "Search for card information")]
    public async Task CardInfoAsync(
        [Summary("name", "Card name")] 
        [Autocomplete(typeof(CardAutocompleteHandler))] string cardName)
    {
        var cards = await _client.GetCardsAsync();
        var card = cards.FirstOrDefault(c => c.Name.ToLower() == cardName.ToLower());

        if (card == null)
        {
            await RespondAsync($"❌ Card **{cardName}** not found.", ephemeral: true);
            return;
        }

        var rarityColor = card.Rarity switch
        {
            Shared.Models.Rarity.Common => Color.LightGrey,
            Shared.Models.Rarity.Rare => Color.Blue,
            Shared.Models.Rarity.Legendary => Color.Gold,
            _ => Color.Default
        };

        var embed = new EmbedBuilder()
            .WithTitle(card.Name)
            .WithDescription($"**Rarity:** {card.Rarity}")
            .AddField("⚔️ Attack", card.Attack.ToString(), inline: true)
            .AddField("🛡️ Defense", card.Defense.ToString(), inline: true)
            .AddField("💥 Damage", $"{card.MinDamage}-{card.MaxDamage}", inline: true)
            .WithColor(rarityColor);

        if (card.ExclusivePackId.HasValue)
        {
            embed.AddField("📦 Availability", "Exclusive to a specific pack.");
        }
        else
        {
            embed.AddField("📦 Availability", "All packs (Global Pool)");
        }

        if (!string.IsNullOrEmpty(card.ImagePath))
        {
            embed.WithImageUrl($"{_client.BaseUrl}/api/images/{card.ImagePath}");
        }

        await RespondAsync(embed: embed.Build());
    }
}
