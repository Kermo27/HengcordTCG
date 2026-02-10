using Discord;
using Discord.Interactions;
using HengcordTCG.Shared.Clients;

namespace HengcordTCG.Bot.Commands;

public class EconomyCommands : InteractionModuleBase<SocketInteractionContext>
{
    private readonly HengcordTCGClient _client;

    public EconomyCommands(HengcordTCGClient client)
    {
        _client = client;
    }

    [SlashCommand("balance", "Sprawdź stan konta")]
    public async Task BalanceAsync()
    {
        var user = await _client.GetUserAsync(Context.User.Id);
        var gold = user?.Gold ?? 0;
        
        var embed = new EmbedBuilder()
            .WithTitle("💰 Stan konta")
            .WithDescription($"**{Context.User.Username}**, posiadasz **{gold}** sztuk złota.")
            .WithColor(Color.Gold)
            .Build();

        await RespondAsync(embed: embed);
    }

    [SlashCommand("daily", "Odbierz codzienną nagrodę")]
    public async Task DailyAsync()
    {
        var result = await _client.ClaimDailyAsync(Context.User.Id, Context.User.Username);

        if (result.Success)
        {
            await RespondAsync($"🌞 **{Context.User.Username}**, odebrałeś nagrodę dzienną: **{result.Amount}** 🪙!");
        }
        else
        {
            var msg = result.TimeRemaining != null 
                ? $"⏳ Musisz poczekać jeszcze: **{result.TimeRemaining}**." 
                : "❌ Wystąpił błąd podczas odbierania nagrody.";
            await RespondAsync($"**{Context.User.Username}**, {msg}", ephemeral: true);
        }
    }
}
