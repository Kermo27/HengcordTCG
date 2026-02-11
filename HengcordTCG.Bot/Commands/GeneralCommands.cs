using Discord;
using Discord.Interactions;
using System.Linq;

namespace HengcordTCG.Bot.Commands;

public class GeneralCommands : InteractionModuleBase<SocketInteractionContext>
{
    private readonly InteractionService _interactions;

    public GeneralCommands(InteractionService interactions)
    {
        _interactions = interactions;
    }

    [SlashCommand("help", "Display list of all commands")]
    public async Task HelpAsync()
    {
        var commands = _interactions.SlashCommands;
        
        var embed = new EmbedBuilder()
            .WithTitle("📜 Command List")
            .WithDescription("Here is the list of all available bot commands:")
            .WithColor(Color.Blue);

        foreach (var cmd in commands.OrderBy(c => c.Name))
        {
            var name = string.IsNullOrEmpty(cmd.Module.SlashGroupName) 
                ? $"/{cmd.Name}" 
                : $"/{cmd.Module.SlashGroupName} {cmd.Name}";

            embed.AddField(name, cmd.Description ?? "No description", inline: false);
        }

        await RespondAsync(embed: embed.Build(), ephemeral: true);
    }

    [SlashCommand("ping", "Check if bot is working")]
    public async Task PingAsync()
    {
        await RespondAsync("Pong! 🏓");
    }

    [SlashCommand("info", "Display bot information")]
    public async Task InfoAsync()
    {
        await RespondAsync("🤖 **HengcordTCG** - Trading Card Game Bot\nVersion: 1.0.0");
    }
}
