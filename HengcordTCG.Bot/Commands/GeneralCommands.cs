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

    [SlashCommand("help", "Wyświetla listę wszystkich komend")]
    public async Task HelpAsync()
    {
        var commands = _interactions.SlashCommands;
        
        var embed = new EmbedBuilder()
            .WithTitle("📜 Lista Komend")
            .WithDescription("Oto lista wszystkich dostępnych komend bota:")
            .WithColor(Color.Blue);

        foreach (var cmd in commands.OrderBy(c => c.Name))
        {
            var name = string.IsNullOrEmpty(cmd.Module.SlashGroupName) 
                ? $"/{cmd.Name}" 
                : $"/{cmd.Module.SlashGroupName} {cmd.Name}";

            embed.AddField(name, cmd.Description ?? "Brak opisu", inline: false);
        }

        await RespondAsync(embed: embed.Build(), ephemeral: true);
    }

    [SlashCommand("ping", "Sprawdza czy bot działa")]
    public async Task PingAsync()
    {
        await RespondAsync("Pong! 🏓");
    }

    [SlashCommand("info", "Wyświetla informacje o bocie")]
    public async Task InfoAsync()
    {
        await RespondAsync("🤖 **HengcordTCG** - Bot do gry karcianej\nWersja: 1.0.0");
    }
}
