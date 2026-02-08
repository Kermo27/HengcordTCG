using Discord;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore;
using HengcordTCG.Bot.Handlers;
using HengcordTCG.Shared.Data;
using HengcordTCG.Shared.Models;

namespace HengcordTCG.Bot.Commands;

[Group("admin", "Komendy administracyjne bota")]
[RequireBotAdmin]
[DefaultMemberPermissions(GuildPermission.Administrator)]
public class AdminCommands : InteractionModuleBase<SocketInteractionContext>
{
    private readonly AppDbContext _db;

    public AdminCommands(AppDbContext db)
    {
        _db = db;
    }

    [SlashCommand("addcard", "Dodaje nową kartę do gry")]
    public async Task AddCardAsync(
        [Summary("nazwa", "Nazwa karty")] string name,
        [Summary("atak", "Wartość ataku")] int attack,
        [Summary("obrona", "Wartość obrony")] int defense,
        [Summary("rzadkosc", "Rzadkość karty")] Rarity rarity = Rarity.Common,
        [Summary("obrazek", "Link do obrazka (opcjonalny)")] string? imageUrl = null)
    {
        var existingCard = await _db.Cards.FirstOrDefaultAsync(c => c.Name == name);
        if (existingCard != null)
        {
            await RespondAsync($"❌ Karta o nazwie '{name}' już istnieje!", ephemeral: true);
            return;
        }

        var card = new Card
        {
            Name = name,
            Attack = attack,
            Defense = defense,
            Rarity = rarity,
            ImageUrl = imageUrl,
            CreatedAt = DateTime.UtcNow
        };

        _db.Cards.Add(card);
        await _db.SaveChangesAsync();

        await RespondAsync($"✅ Dodano kartę: **{name}** (ATK: {attack}, DEF: {defense}, Rarity: {rarity})");
    }

    [SlashCommand("removecard", "Usuwa kartę z gry")]
    public async Task RemoveCardAsync([Summary("nazwa", "Nazwa karty")] string name)
    {
        var card = await _db.Cards.FirstOrDefaultAsync(c => c.Name == name);
        
        if (card == null)
        {
            await RespondAsync($"❌ Nie znaleziono karty o nazwie '{name}'", ephemeral: true);
            return;
        }

        _db.Cards.Remove(card);
        await _db.SaveChangesAsync();

        await RespondAsync($"🗑️ Usunięto kartę: **{name}**");
    }

    [SlashCommand("listcards", "Wyświetla listę kart")]
    public async Task ListCardsAsync()
    {
        var cards = await _db.Cards.ToListAsync();
        
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
        await RespondAsync("🔄 (To polecenie na razie nic nie robi, bo EF Core pobiera dane na bieżąco)");
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

        var dbUser = await _db.Users.FirstOrDefaultAsync(u => u.DiscordId == user.Id);
        if (dbUser == null)
        {
            dbUser = new User
            {
                DiscordId = user.Id,
                Username = user.Username,
                CreatedAt = DateTime.UtcNow,
                LastSeen = DateTime.UtcNow
            };
            _db.Users.Add(dbUser);
        }

        dbUser.Gold += amount;
        await _db.SaveChangesAsync();

        await RespondAsync($"✅ Dodano **{amount}** złota dla **{user.Username}**. Nowy balans: **{dbUser.Gold}**.");
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

        var dbUser = await _db.Users.FirstOrDefaultAsync(u => u.DiscordId == user.Id);
        if (dbUser == null)
        {
            dbUser = new User
            {
                DiscordId = user.Id,
                Username = user.Username,
                CreatedAt = DateTime.UtcNow,
                LastSeen = DateTime.UtcNow
            };
            _db.Users.Add(dbUser);
        }

        dbUser.Gold = amount;
        await _db.SaveChangesAsync();

        await RespondAsync($"✅ Ustawiono balans **{user.Username}** na **{amount}** złota.");
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

        var existingPack = await _db.PackTypes.FirstOrDefaultAsync(p => p.Name == name);
        if (existingPack != null)
        {
            await RespondAsync($"❌ Paczka o nazwie '{name}' już istnieje!");
            return;
        }

        var pack = new PackType
        {
            Name = name,
            Price = price,
            ChanceCommon = common,
            ChanceRare = rare,
            ChanceLegendary = legendary
        };

        _db.PackTypes.Add(pack);
        await _db.SaveChangesAsync();

        await RespondAsync($"✅ Utworzono paczkę **{name}** (Cena: {price}).\nSzans: C:{common} R:{rare} L:{legendary}");
    }

    [SlashCommand("listpacks", "Lista dostępnych paczek")]
    public async Task ListPacksAsync()
    {
        var packs = await _db.PackTypes.ToListAsync();
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
        var card = await _db.Cards.FirstOrDefaultAsync(c => c.Name == cardName);
        if (card == null)
        {
            await RespondAsync($"❌ Nie znaleziono karty '{cardName}'!", ephemeral: true);
            return;
        }

        if (packName.ToLower() == "null" || packName.ToLower() == "default" || packName.ToLower() == "base")
        {
            card.ExclusivePackId = null;
            await _db.SaveChangesAsync();
            await RespondAsync($"✅ Karta **{card.Name}** jest teraz dostępna we wszystkich paczkach (Global Pool).");
            return;
        }

        var pack = await _db.PackTypes.FirstOrDefaultAsync(p => p.Name == packName);
        if (pack == null)
        {
            await RespondAsync($"❌ Nie znaleziono paczki '{packName}'!", ephemeral: true);
            return;
        }

        card.ExclusivePack = pack;
        await _db.SaveChangesAsync();

        await RespondAsync($"✅ Karta **{card.Name}** została przypisana ekskluzywnie do paczki **{pack.Name}**.");
    }

    [SlashCommand("togglepack", "Włącza/wyłącza dostępność paczki")]
    public async Task TogglePackAsync(
        [Summary("paczka", "Nazwa paczki")] string packName)
    {
        var pack = await _db.PackTypes.FirstOrDefaultAsync(p => p.Name == packName);
        if (pack == null)
        {
            await RespondAsync($"❌ Nie znaleziono paczki '{packName}'!", ephemeral: true);
            return;
        }

        pack.IsActive = !pack.IsActive;
        await _db.SaveChangesAsync();

        var status = pack.IsActive ? "🟢 AKTYWNA" : "🔴 NIEAKTYWNA";
        await RespondAsync($"✅ Paczka **{pack.Name}** jest teraz {status}.");
    }

    [SlashCommand("fixinventory", "Naprawia zduplikowane karty w ekwipunku")]
    public async Task FixInventoryAsync()
    {
        var allUserCards = await _db.UserCards.ToListAsync();
        
        var duplicates = allUserCards
            .GroupBy(uc => new { uc.UserId, uc.CardId })
            .Where(g => g.Count() > 1)
            .ToList();

        if (duplicates.Count == 0)
        {
            await RespondAsync("✅ Nie znaleziono duplikatów.");
            return;
        }

        int fixedCount = 0;

        foreach (var group in duplicates)
        {
            var cards = group.OrderBy(uc => uc.ObtainedAt).ToList();
            var primary = cards.First();
            
            int totalCount = cards.Sum(c => c.Count);
            primary.Count = totalCount;
            
            foreach (var duplicate in cards.Skip(1))
            {
                _db.UserCards.Remove(duplicate);
            }
            
            fixedCount++;
        }

        await _db.SaveChangesAsync();
        await RespondAsync($"✅ Naprawiono **{fixedCount}** zduplikowanych wpisów.");
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

        var card = await _db.Cards.FirstOrDefaultAsync(c => c.Name == cardName);
        if (card == null)
        {
            await RespondAsync($"❌ Nie znaleziono karty '{cardName}'!", ephemeral: true);
            return;
        }

        var dbUser = await _db.Users
            .Include(u => u.UserCards)
            .FirstOrDefaultAsync(u => u.DiscordId == user.Id);

        if (dbUser == null)
        {
            dbUser = new User
            {
                DiscordId = user.Id,
                Username = user.Username,
                CreatedAt = DateTime.UtcNow,
                LastSeen = DateTime.UtcNow
            };
            _db.Users.Add(dbUser);
        }

        var userCard = dbUser.UserCards.FirstOrDefault(uc => uc.CardId == card.Id);
        if (userCard != null)
        {
            userCard.Count += amount;
        }
        else
        {
            dbUser.UserCards.Add(new UserCard
            {
                CardId = card.Id,
                Count = amount,
                ObtainedAt = DateTime.UtcNow,
            });
        }

        await _db.SaveChangesAsync();

        await RespondAsync($"✅ Przekazano **{amount}x {card.Name}** użytkownikowi **{user.Username}**.");
    }

    [SlashCommand("addadmin", "Dodaje uprawnienia admina użytkownikowi")]
    public async Task AddAdminAsync([Summary("uzytkownik", "Użytkownik")] Discord.IUser user)
    {
        var dbUser = await _db.Users.FirstOrDefaultAsync(u => u.DiscordId == user.Id);
        if (dbUser == null)
        {
            dbUser = new User
            {
                DiscordId = user.Id,
                Username = user.Username,
                CreatedAt = DateTime.UtcNow,
                LastSeen = DateTime.UtcNow
            };
            _db.Users.Add(dbUser);
        }

        if (dbUser.IsBotAdmin)
        {
            await RespondAsync($"ℹ️ **{user.Username}** jest już adminem.", ephemeral: true);
            return;
        }

        dbUser.IsBotAdmin = true;
        await _db.SaveChangesAsync();

        await RespondAsync($"✅ Nadano uprawnienia admina użytkownikowi **{user.Username}**.");
    }

    [SlashCommand("removeadmin", "Usuwa uprawnienia admina użytkownikowi")]
    public async Task RemoveAdminAsync([Summary("uzytkownik", "Użytkownik")] Discord.IUser user)
    {
        var dbUser = await _db.Users.FirstOrDefaultAsync(u => u.DiscordId == user.Id);
        
        if (dbUser == null || !dbUser.IsBotAdmin)
        {
            await RespondAsync($"❌ **{user.Username}** nie jest adminem.", ephemeral: true);
            return;
        }

        dbUser.IsBotAdmin = false;
        await _db.SaveChangesAsync();

        await RespondAsync($"✅ Usunięto uprawnienia admina użytkownikowi **{user.Username}**.");
    }
}
