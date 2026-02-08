using Discord;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore;
using HengcordTCG.Shared.Data;
using HengcordTCG.Bot.Handlers;

namespace HengcordTCG.Bot.Commands;

public class InfoCommands : InteractionModuleBase<SocketInteractionContext>
{
    private readonly AppDbContext _db;

    public InfoCommands(AppDbContext db)
    {
        _db = db;
    }

    [SlashCommand("card", "Wyszukaj informacje o karcie")]
    public async Task CardInfoAsync(
        [Summary("nazwa", "Nazwa karty")] 
        [Autocomplete(typeof(CardAutocompleteHandler))] string cardName)
    {
        var card = await _db.Cards
            .FirstOrDefaultAsync(c => c.Name.ToLower() == cardName.ToLower());

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
            var pack = await _db.PackTypes.FindAsync(card.ExclusivePackId.Value);
            if (pack != null)
            {
                embed.AddField("📦 Dostępność", $"Wyłącznie w pacce: **{pack.Name}**");
            }
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
