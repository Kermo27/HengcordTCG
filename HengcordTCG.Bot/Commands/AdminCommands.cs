using Discord;
using Discord.Interactions;
using HengcordTCG.Bot.Handlers;
using HengcordTCG.Shared.Clients;
using HengcordTCG.Shared.Models;

namespace HengcordTCG.Bot.Commands;

[Group("admin", "Komendy administracyjne bota")]
[RequireBotAdmin]
[DefaultMemberPermissions(GuildPermission.Administrator)]
public class AdminCommands : InteractionModuleBase<SocketInteractionContext>
{
    private readonly HengcordTCGClient _client;

    public AdminCommands(HengcordTCGClient client)
    {
        _client = client;
    }

    [SlashCommand("addcard", "Dodaje nową kartę do gry")]
    public async Task AddCardAsync(
        [Summary("nazwa", "Nazwa karty")] string name,
        [Summary("atak", "Wartość ataku")] int attack,
        [Summary("obrona", "Wartość obrony")] int defense,
        [Summary("rzadkosc", "Rzadkość karty")] Rarity rarity = Rarity.Common,
        [Summary("obrazek", "Link do obrazka (opcjonalny)")] string? imageUrl = null)
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
            await RespondAsync($"✅ Dodano kartę: **{name}** (ATK: {attack}, DEF: {defense}, Rarity: {rarity})");
        else
            await RespondAsync($"❌ Nie udało się dodać karty '{name}'.", ephemeral: true);
    }

    [SlashCommand("removecard", "Usuwa kartę z gry")]
    public async Task RemoveCardAsync([Summary("nazwa", "Nazwa karty")] string name)
    {
        var success = await _client.RemoveCardAsync(name);
        if (success)
            await RespondAsync($"🗑️ Usunięto kartę: **{name}**");
        else
            await RespondAsync($"❌ Nie znaleziono lub nie udało się usunąć karty '{name}'", ephemeral: true);
    }

    [SlashCommand("listcards", "Wyświetla listę kart")]
    public async Task ListCardsAsync()
    {
        var cards = await _client.GetCardsAsync();
        
        if (cards.Count == 0)
        {
            await RespondAsync("📭 Baza kart jest pusta.");
            return;
        }

        var description = string.Join("\n", cards.Select(c => $"- **{c.Name}** (ATK: {c.Attack}, DEF: {c.Defense})"));
        
        if (description.Length > 1900)
            description = description.Substring(0, 1900) + "... (i więcej)";

        await RespondAsync($"📚 **Lista kart ({cards.Count}):**\n{description}");
    }

    [SlashCommand("reload", "Przeładowuje dane bota")]
    public async Task ReloadAsync()
    {
        await RespondAsync("🔄 Dane są pobierane z API na bieżąco.");
    }

    [SlashCommand("givegold", "Daje złoto użytkownikowi")]
    public async Task GiveGoldAsync(
        [Summary("uzytkownik", "Użytkownik")] Discord.IUser user,
        [Summary("ilosc", "Ilość złota")] int amount)
    {
        if (amount <= 0)
        {
            await RespondAsync("❌ Ilość musi być dodatnia!");
            return;
        }

        var newBalance = await _client.GiveGoldAdminAsync(user.Id, amount);
        if (newBalance != -1)
            await RespondAsync($"✅ Dodano **{amount}** złota dla **{user.Username}**. Nowy balans: **{newBalance}**.");
        else
            await RespondAsync($"❌ Nie udało się dodać złota dla **{user.Username}**.", ephemeral: true);
    }

    [SlashCommand("setgold", "Ustawia złoto użytkownikowi")]
    public async Task SetGoldAsync(
        [Summary("uzytkownik", "Użytkownik")] Discord.IUser user,
        [Summary("ilosc", "Ilość złota")] int amount)
    {
        if (amount < 0)
        {
            await RespondAsync("❌ Ilość nie może być ujemna!");
            return;
        }

        var newBalance = await _client.SetGoldAdminAsync(user.Id, amount);
        if (newBalance != -1)
            await RespondAsync($"✅ Ustawiono balans **{user.Username}** na **{newBalance}** złota.");
        else
            await RespondAsync($"❌ Nie udało się ustawić złota dla **{user.Username}**.", ephemeral: true);
    }

    [SlashCommand("createpack", "Tworzy nowy typ paczki")]
    public async Task CreatePackAsync(
        [Summary("nazwa", "Nazwa paczki")] string name,
        [Summary("cena", "Cena paczki")] int price,
        [Summary("common", "Szansa na Common (waga)")] int common,
        [Summary("rare", "Szansa na Rare (waga)")] int rare,
        [Summary("legendary", "Szansa na Legendary (waga)")] int legendary)
    {
        if (price <= 0)
        {
            await RespondAsync("❌ Cena musi być dodatnia!");
            return;
        }

        var pack = new PackType
        {
            Name = name,
            Price = price,
            ChanceCommon = common,
            ChanceRare = rare,
            ChanceLegendary = legendary,
            IsActive = true
        };

        var success = await _client.CreatePackAsync(pack);
        if (success)
            await RespondAsync($"✅ Utworzono paczkę **{name}** (Cena: {price}).\nSzans: C:{common} R:{rare} L:{legendary}");
        else
            await RespondAsync($"❌ Nie udało się utworzyć paczki '{name}'.", ephemeral: true);
    }

    [SlashCommand("listpacks", "Lista dostępnych paczek")]
    public async Task ListPacksAsync()
    {
        var packs = await _client.GetPacksAsync();
        if (packs.Count == 0)
        {
            await RespondAsync("Brak paczek w bazie.");
            return;
        }

        var description = string.Join("\n", packs.Select(p => $"- **{p.Name}** ({p.Price}g) [C:{p.ChanceCommon}% R:{p.ChanceRare}% L:{p.ChanceLegendary}%]"));
        
        var embed = new EmbedBuilder()
            .WithTitle("📦 Dostępne paczki")
            .WithDescription(description)
            .WithColor(Color.Blue)
            .Build();

        await RespondAsync(embed: embed);
    }

    [SlashCommand("setcardpack", "Przypisuje kartę do paczki (lub usuwa przypisanie)")]
    public async Task SetCardPackAsync(
        [Summary("karta", "Nazwa karty")] string cardName,
        [Summary("paczka", "Nazwa paczki (wpisz 'null' aby usunąć)")] string packName)
    {
        var success = await _client.SetCardPackAsync(cardName, packName);
        if (success)
            await RespondAsync($"✅ Zaktualizowano przypisanie karty **{cardName}**.");
        else
            await RespondAsync($"❌ Nie udało się zaktualizować przypisania karty '{cardName}'.", ephemeral: true);
    }

    [SlashCommand("togglepack", "Włącza/wyłącza dostępność paczki")]
    public async Task TogglePackAsync(
        [Summary("paczka", "Nazwa paczki")] string packName)
    {
        var success = await _client.TogglePackAsync(packName);
        if (success)
            await RespondAsync($"✅ Zmieniono dostępność paczki **{packName}**.");
        else
            await RespondAsync($"❌ Nie znaleziono paczki '{packName}'!", ephemeral: true);
    }

    [SlashCommand("fixinventory", "Naprawia zduplikowane karty w ekwipunku")]
    public async Task FixInventoryAsync()
    {
        var success = await _client.FixInventoryAsync();
        if (success)
            await RespondAsync("✅ Naprawiono zduplikowane wpisy w ekwipunku.");
        else
            await RespondAsync("❌ Błąd podczas naprawy ekwipunku.", ephemeral: true);
    }

    [SlashCommand("givecard", "Daje kartę użytkownikowi")]
    public async Task GiveCardAsync(
        [Summary("uzytkownik", "Użytkownik")] Discord.IUser user,
        [Summary("karta", "Nazwa karty")] [Autocomplete(typeof(CardAutocompleteHandler))] string cardName,
        [Summary("ilosc", "Ilość (domyślnie 1)")] int amount = 1)
    {
        if (amount <= 0)
        {
            await RespondAsync("❌ Ilość musi być dodatnia!", ephemeral: true);
            return;
        }

        var success = await _client.GiveCardAsync(user.Id, cardName, amount);
        if (success)
            await RespondAsync($"✅ Przekazano **{amount}x {cardName}** użytkownikowi **{user.Username}**.");
        else
            await RespondAsync($"❌ Nie udało się przekazać karty '{cardName}'!", ephemeral: true);
    }

    [SlashCommand("addadmin", "Dodaje uprawnienia admina użytkownikowi")]
    public async Task AddAdminAsync([Summary("uzytkownik", "Użytkownik")] Discord.IUser user)
    {
        var success = await _client.AddAdminAsync(user.Id);
        if (success)
            await RespondAsync($"✅ Nadano uprawnienia admina użytkownikowi **{user.Username}**.");
        else
            await RespondAsync($"❌ Nie udało się nadać uprawnień admina dla **{user.Username}**.", ephemeral: true);
    }

    [SlashCommand("removeadmin", "Usuwa uprawnienia admina użytkownikowi")]
    public async Task RemoveAdminAsync([Summary("uzytkownik", "Użytkownik")] Discord.IUser user)
    {
        var success = await _client.RemoveAdminAsync(user.Id);
        if (success)
            await RespondAsync($"✅ Usunięto uprawnienia admina użytkownikowi **{user.Username}**.");
        else
            await RespondAsync($"❌ Nie udało się usunąć uprawnień admina dla **{user.Username}**.", ephemeral: true);
    }
}
