using Discord;
using Discord.Interactions;
using HengcordTCG.Bot.Handlers;
using HengcordTCG.Shared.Clients;

namespace HengcordTCG.Bot.Commands;

public class ShopCommands : InteractionModuleBase<SocketInteractionContext>
{
    private readonly HengcordTCGClient _client;

    public ShopCommands(HengcordTCGClient client)
    {
        _client = client;
    }

    [SlashCommand("buy", "Buy a card pack")]
    public async Task BuyPackAsync(
        [Summary("pack", "Pack name (default: Base Set)")]
        [Autocomplete(typeof(Handlers.PackAutocompleteHandler))] string packName = "Base Set")
    {
        var result = await _client.BuyPackAsync(Context.User.Id, Context.User.Username, packName);

        if (!result.success || result.cards == null)
        {
            await RespondAsync($"❌ {result.message}", ephemeral: true);
            return;
        }

        var embed = new EmbedBuilder()
            .WithTitle($"📦 Opened Pack: {packName}")
            .WithColor(Color.Purple)
            .WithDescription($"You received {result.cards.Count} cards:");

        foreach (var card in result.cards)
        {
            embed.AddField($"{card.Name} ({card.Rarity})", $"ATK: {card.Attack} DEF: {card.Defense}", inline: true);
        }

        await RespondAsync(embed: embed.Build());
    }

    [SlashCommand("collection", "Show your cards")]
    public async Task ShowCollectionAsync(
        [Summary("view", "Select view (default: cards)")] 
        [Choice("Cards (Interactive)", "cards")] 
        [Choice("List (Text)", "list")] string view = "cards")
    {
        if (view == "list")
        {
            await SendCollectionListAsync(Context.User.Id);
        }
        else
        {
            // Start session with current time + 2 minutes
            long expiryTicks = DateTime.UtcNow.AddMinutes(2).Ticks;
            await SendCollectionPageAsync(Context.User.Id, 0, expiryTicks, isNewMessage: true);
        }
    }

    private async Task SendCollectionListAsync(ulong ownerId)
    {
        var collection = await _client.GetCollectionAsync(ownerId);

        if (collection.Count == 0)
        {
            await RespondAsync("📭 You don't have any cards yet. Use `/buy pack`!", ephemeral: true);
            return;
        }

        var sortedCollection = collection
            .OrderByDescending(c => c.Card.Rarity)
            .ThenBy(c => c.Card.Name)
            .ToList();

        var description = string.Join("\n", sortedCollection.Select(uc => 
            $"`{uc.Count}x` **{uc.Card.Name}** ({uc.Card.Rarity})"));

        if (description.Length > 4000) 
            description = description.Substring(0, 3950) + "\n... (list too long)";

        var embed = new EmbedBuilder()
            .WithTitle($"📜 Card List: {Context.User.Username}")
            .WithDescription(description)
            .WithColor(Color.Blue)
            .WithFooter($"Total: {sortedCollection.Sum(x => x.Count)} cards | {sortedCollection.Count} unique")
            .Build();

        await RespondAsync(embed: embed);
    }

    [ComponentInteraction("collection_nav:*:*:*")]
    public async Task HandleCollectionNav(string ownerIdStr, string pageStr, string expiryStr)
    {
        if (!ulong.TryParse(ownerIdStr, out ulong ownerId) || 
            !int.TryParse(pageStr, out int page) ||
            !long.TryParse(expiryStr, out long expiryTicks))
            return;

        await SendCollectionPageAsync(ownerId, page, expiryTicks, isNewMessage: false);
    }

    private async Task SendCollectionPageAsync(ulong ownerId, int page, long expiryTicks, bool isNewMessage)
    {
        if (!isNewMessage)
        {
            var expiryDate = new DateTime(expiryTicks);
            if (DateTime.UtcNow > expiryDate)
            {
                if (Context.Interaction is Discord.WebSocket.SocketMessageComponent component)
                {
                    await component.UpdateAsync(msg =>
                    {
                        msg.Components = new ComponentBuilder().Build(); // Remove buttons
                        msg.Content = "⚠️ **Session expired.** Use `/collection` again.";
                    });
                }
                return;
            }
        }
        
        long newExpiryTicks = DateTime.UtcNow.AddMinutes(2).Ticks;

        var collection = await _client.GetCollectionAsync(ownerId);

        if (collection.Count == 0)
        {
            await RespondAsync("📭 You don't have any cards yet. Use `/buy pack`!", ephemeral: true);
            return;
        }
        
        var sortedCollection = collection
            .OrderByDescending(c => c.Card.Rarity)
            .ThenBy(c => c.Card.Name)
            .ToList();
        
        if (page < 0) page = 0;
        if (page >= sortedCollection.Count) page = sortedCollection.Count - 1;

        var userCard = sortedCollection[page];
        var card = userCard.Card;
        
        long expirySeconds = new DateTimeOffset(expiryTicks, TimeSpan.Zero).ToUnixTimeSeconds();

        var embed = new EmbedBuilder()
            .WithTitle(card.Name)
            .WithDescription($"**Rarity:** {card.Rarity}\n**Count:** {userCard.Count}\n⏳ Expires: <t:{expirySeconds}:R>")
            .AddField("⚔️ Attack", card.Attack.ToString(), inline: true)
            .AddField("🛡️ Defense", card.Defense.ToString(), inline: true)
            .WithFooter($"Card {page + 1}/{sortedCollection.Count}")
            .WithColor(GetRarityColor(card.Rarity));

        if (!string.IsNullOrEmpty(card.ImageUrl))
        {
            embed.WithImageUrl(card.ImageUrl);
        }
        
        var components = new ComponentBuilder()
            .WithButton("◀", $"collection_nav:{ownerId}:{page - 1}:{newExpiryTicks}", ButtonStyle.Secondary, disabled: page == 0)
            .WithButton("▶", $"collection_nav:{ownerId}:{page + 1}:{newExpiryTicks}", ButtonStyle.Secondary, disabled: page >= sortedCollection.Count - 1)
            .Build();

        if (isNewMessage)
        {
            await RespondAsync(embed: embed.Build(), components: components);
        }
        else
        {
            // Cast to SocketMessageComponent to update
            if (Context.Interaction is Discord.WebSocket.SocketMessageComponent component)
            {
                await component.UpdateAsync(msg =>
                {
                    msg.Embed = embed.Build();
                    msg.Components = components;
                    msg.Content = null; // Clear potential expiration message if any
                });
            }
        }
    }

    private Color GetRarityColor(Shared.Models.Rarity rarity)
    {
        return rarity switch
        {
            Shared.Models.Rarity.Common => Color.LightGrey,
            Shared.Models.Rarity.Rare => Color.Blue,
            Shared.Models.Rarity.Legendary => Color.Gold,
            _ => Color.Default
        };
    }
}
