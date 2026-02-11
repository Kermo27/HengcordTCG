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

    [SlashCommand("balance", "Check your account balance")]
    public async Task BalanceAsync()
    {
        var user = await _client.GetUserAsync(Context.User.Id);
        var gold = user?.Gold ?? 0;
        
        var embed = new EmbedBuilder()
            .WithTitle("💰 Account Balance")
            .WithDescription($"**{Context.User.Username}**, you have **{gold}** gold.")
            .WithColor(Color.Gold)
            .Build();

        await RespondAsync(embed: embed);
    }

    [SlashCommand("daily", "Claim your daily reward")]
    public async Task DailyAsync()
    {
        var result = await _client.ClaimDailyAsync(Context.User.Id, Context.User.Username);

        if (result.Success)
        {
            await RespondAsync($"🌞 **{Context.User.Username}**, you claimed your daily reward: **{result.Amount}** 🪙!");
        }
        else
        {
            var msg = result.TimeRemaining != null 
                ? $"⏳ You need to wait: **{result.TimeRemaining}**." 
                : "❌ An error occurred while claiming your reward.";
            await RespondAsync($"**{Context.User.Username}**, {msg}", ephemeral: true);
        }
    }
}
