using Discord;
using Discord.Interactions;
using HengcordTCG.Bot.Handlers;
using HengcordTCG.Shared.Clients;
using HengcordTCG.Shared.Models;

namespace HengcordTCG.Bot.Commands;

[Group("admin", "Bot administrative commands")]
[RequireBotAdmin]
[DefaultMemberPermissions(GuildPermission.Administrator)]
public class AdminCommands : InteractionModuleBase<SocketInteractionContext>
{
    private readonly HengcordTCGClient _client;

    public AdminCommands(HengcordTCGClient client)
    {
        _client = client;
    }

    [SlashCommand("addcard", "Add a new card to the game")]
    public async Task AddCardAsync(
        [Summary("name", "Card name")] string name,
        [Summary("attack", "Attack value")] int attack,
        [Summary("defense", "Defense value")] int defense,
        [Summary("rarity", "Card rarity")] Rarity rarity = Rarity.Common,
        [Summary("image", "Image URL (optional)")] string? imageUrl = null)
    {
        var card = new Card
        {
            Name = name,
            Attack = attack,
            Defense = defense,
            Rarity = rarity,
            ImageUrl = imageUrl,
            CreatedAt = DateTime.UtcNow
        };

        var success = await _client.AddCardAsync(card);
        if (success)
            await RespondAsync($"✅ Added card: **{name}** (ATK: {attack}, DEF: {defense}, Rarity: {rarity})");
        else
            await RespondAsync($"❌ Failed to add card '{name}'.", ephemeral: true);
    }

    [SlashCommand("removecard", "Remove a card from the game")]
    public async Task RemoveCardAsync([Summary("name", "Card name")] string name)
    {
        var success = await _client.RemoveCardAsync(name);
        if (success)
            await RespondAsync($"🗑️ Removed card: **{name}**");
        else
            await RespondAsync($"❌ Card '{name}' not found or could not be removed", ephemeral: true);
    }

    [SlashCommand("listcards", "Display list of cards")]
    public async Task ListCardsAsync()
    {
        var cards = await _client.GetCardsAsync();
        
        if (cards.Count == 0)
        {
            await RespondAsync("📭 Card database is empty.");
            return;
        }

        var description = string.Join("\n", cards.Select(c => $"- **{c.Name}** (ATK: {c.Attack}, DEF: {c.Defense})"));
        
        if (description.Length > 1900)
            description = description.Substring(0, 1900) + "... (and more)";

        await RespondAsync($"📚 **Card List ({cards.Count}):**\n{description}");
    }

    [SlashCommand("reload", "Reload bot data")]
    public async Task ReloadAsync()
    {
        await RespondAsync("🔄 Data is fetched from API in real-time.");
    }

    [SlashCommand("givegold", "Give gold to a user")]
    public async Task GiveGoldAsync(
        [Summary("user", "User")] Discord.IUser user,
        [Summary("amount", "Amount of gold")] int amount)
    {
        if (amount <= 0)
        {
            await RespondAsync("❌ Amount must be positive!");
            return;
        }

        var newBalance = await _client.GiveGoldAdminAsync(user.Id, amount);
        if (newBalance != -1)
            await RespondAsync($"✅ Added **{amount}** gold to **{user.Username}**. New balance: **{newBalance}**");
        else
            await RespondAsync($"❌ Failed to add gold to **{user.Username}**.", ephemeral: true);
    }

    [SlashCommand("setgold", "Set user's gold amount")]
    public async Task SetGoldAsync(
        [Summary("user", "User")] Discord.IUser user,
        [Summary("amount", "Amount of gold")] int amount)
    {
        if (amount < 0)
        {
            await RespondAsync("❌ Amount cannot be negative!");
            return;
        }

        var newBalance = await _client.SetGoldAdminAsync(user.Id, amount);
        if (newBalance != -1)
            await RespondAsync($"✅ Set **{user.Username}**'s balance to **{newBalance}** gold.");
        else
            await RespondAsync($"❌ Failed to set gold for **{user.Username}**.", ephemeral: true);
    }

    [SlashCommand("createpack", "Create a new pack type")]
    public async Task CreatePackAsync(
        [Summary("name", "Pack name")] string name,
        [Summary("price", "Pack price")] int price,
        [Summary("common", "Chance for Common (weight)")] int common,
        [Summary("rare", "Chance for Rare (weight)")] int rare,
        [Summary("legendary", "Chance for Legendary (weight)")] int legendary)
    {
        if (price <= 0)
        {
            await RespondAsync("❌ Price must be positive!");
            return;
        }

        var pack = new PackType
        {
            Name = name,
            Price = price,
            ChanceCommon = common,
            ChanceRare = rare,
            ChanceLegendary = legendary,
            IsAvailable = true
        };

        var success = await _client.CreatePackAsync(pack);
        if (success)
            await RespondAsync($"✅ Created pack **{name}** (Price: {price}).\nChances: C:{common} R:{rare} L:{legendary}");
        else
            await RespondAsync($"❌ Failed to create pack '{name}'.", ephemeral: true);
    }

    [SlashCommand("listpacks", "List available packs")]
    public async Task ListPacksAsync()
    {
        var packs = await _client.GetPacksAsync();
        if (packs.Count == 0)
        {
            await RespondAsync("No packs in database.");
            return;
        }

        var description = string.Join("\n", packs.Select(p => $"- **{p.Name}** ({p.Price}g) [C:{p.ChanceCommon}% R:{p.ChanceRare}% L:{p.ChanceLegendary}%]"));
        
        var embed = new EmbedBuilder()
            .WithTitle("📦 Available Packs")
            .WithDescription(description)
            .WithColor(Color.Blue)
            .Build();

        await RespondAsync(embed: embed);
    }

    [SlashCommand("setcardpack", "Assign card to pack (or remove assignment)")]
    public async Task SetCardPackAsync(
        [Summary("card", "Card name")] string cardName,
        [Summary("pack", "Pack name (type 'null' to remove)")] string packName)
    {
        var success = await _client.SetCardPackAsync(cardName, packName);
        if (success)
            await RespondAsync($"✅ Updated card **{cardName}** assignment.");
        else
            await RespondAsync($"❌ Failed to update assignment for card '{cardName}'.", ephemeral: true);
    }

    [SlashCommand("togglepack", "Toggle pack availability")]
    public async Task TogglePackAsync(
        [Summary("pack", "Pack name")] string packName)
    {
        var success = await _client.TogglePackAsync(packName);
        if (success)
            await RespondAsync($"✅ Changed availability for pack **{packName}**");
        else
            await RespondAsync($"❌ Pack '{packName}' not found!", ephemeral: true);
    }

    [SlashCommand("fixinventory", "Fix duplicate cards in inventory")]
    public async Task FixInventoryAsync()
    {
        var success = await _client.FixInventoryAsync();
        if (success)
            await RespondAsync("✅ Fixed duplicate entries in inventory.");
        else
            await RespondAsync("❌ Error while fixing inventory.", ephemeral: true);
    }

    [SlashCommand("givecard", "Give a card to a user")]
    public async Task GiveCardAsync(
        [Summary("user", "User")] Discord.IUser user,
        [Summary("card", "Card name")] [Autocomplete(typeof(CardAutocompleteHandler))] string cardName,
        [Summary("amount", "Amount (default: 1)")] int amount = 1)
    {
        if (amount <= 0)
        {
            await RespondAsync("❌ Amount must be positive!", ephemeral: true);
            return;
        }

        var success = await _client.GiveCardAsync(user.Id, cardName, amount);
        if (success)
            await RespondAsync($"✅ Gave **{amount}x {cardName}** to **{user.Username}**");
        else
            await RespondAsync($"❌ Failed to give card '{cardName}'!", ephemeral: true);
    }

    [SlashCommand("addadmin", "Add bot admin privileges to a user")]
    public async Task AddAdminAsync([Summary("user", "User")] Discord.IUser user)
    {
        var success = await _client.AddAdminAsync(user.Id);
        if (success)
            await RespondAsync($"✅ Granted bot admin privileges to **{user.Username}**");
        else
            await RespondAsync($"❌ Failed to grant admin privileges to **{user.Username}**.", ephemeral: true);
    }

    [SlashCommand("removeadmin", "Remove bot admin privileges from a user")]
    public async Task RemoveAdminAsync([Summary("user", "User")] Discord.IUser user)
    {
        var success = await _client.RemoveAdminAsync(user.Id);
        if (success)
            await RespondAsync($"✅ Removed bot admin privileges from **{user.Username}**");
        else
            await RespondAsync($"❌ Failed to remove admin privileges from **{user.Username}**.", ephemeral: true);
    }
}
