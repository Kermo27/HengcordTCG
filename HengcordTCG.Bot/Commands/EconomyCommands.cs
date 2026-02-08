using Discord;
using Discord.Interactions;
using HengcordTCG.Shared.Services;

namespace HengcordTCG.Bot.Commands;

public class EconomyCommands : InteractionModuleBase<SocketInteractionContext>
{
    private readonly UserService _userService;

    public EconomyCommands(UserService userService)
    {
        _userService = userService;
    }

    [SlashCommand("balance", "Sprawdź stan konta")]
    public async Task BalanceAsync()
    {
        var user = await _userService.GetOrCreateUserAsync(Context.User.Id, Context.User.Username);
        
        var embed = new EmbedBuilder()
            .WithTitle("💰 Stan konta")
            .WithDescription($"**{Context.User.Username}**, posiadasz **{user.Gold}** sztuk złota.")
            .WithColor(Color.Gold)
            .Build();

        await RespondAsync(embed: embed);
    }

    [SlashCommand("daily", "Odbierz codzienną nagrodę")]
    public async Task DailyAsync()
    {
        var result = await _userService.ClaimDailyAsync(Context.User.Id, Context.User.Username);

        if (result.success)
        {
            await RespondAsync($"🌞 **{Context.User.Username}**, odebrałeś nagrodę dzienną!\nOtrzymujesz **{result.amount}** 🪙 złota!");
        }
        else
        {
            var time = result.timeRemaining!.Value;
            var timeStr = $"{(int)time.TotalHours}h {time.Minutes}m";
            await RespondAsync($"⏳ **{Context.User.Username}**, musisz poczekać jeszcze **{timeStr}** na kolejną nagrodę.", ephemeral: true);
        }
    }
}
