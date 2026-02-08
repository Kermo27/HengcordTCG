using Discord.Interactions;

namespace HengcordTCG.Bot.Commands;

public class GeneralCommands : InteractionModuleBase<SocketInteractionContext>
{
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
