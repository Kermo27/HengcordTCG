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

    [SlashCommand("card", "Wyszukaj informacje o karcie")]
    public async Task CardInfoAsync(
        [Summary("nazwa", "Nazwa karty")] 
        [Autocomplete(typeof(CardAutocompleteHandler))] string cardName)
    {
        var cards = await _client.GetCardsAsync();
        var card = cards.FirstOrDefault(c => c.Name.ToLower() == cardName.ToLower());

        if (card == null)
        {
            await RespondAsync($"❌ Nie znaleziono karty o nazwie **{cardName}**.", ephemeral: true);
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
            .WithDescription($"**Rzadkość:** {card.Rarity}")
            .AddField("⚔️ Atak", card.Attack.ToString(), inline: true)
            .AddField("🛡️ Obrona", card.Defense.ToString(), inline: true)
            .WithColor(rarityColor);

        if (card.ExclusivePackId.HasValue)
        {
            // Note: Since we don't have GetPacksAsync in client yet, 
            // we just show that it is exclusive. 
            embed.AddField("📦 Dostępność", "Karta ekskluzywna dla konkretnej paczki.");
        }
        else
        {
            embed.AddField("📦 Dostępność", "Wszystkie paczki (Global Pool)");
        }

        if (!string.IsNullOrEmpty(card.ImageUrl))
        {
            embed.WithImageUrl(card.ImageUrl);
        }

        await RespondAsync(embed: embed.Build());
    }
}
