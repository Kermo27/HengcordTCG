using Discord;
using Discord.Interactions;
using HengcordTCG.Shared.Clients;

namespace HengcordTCG.Bot.Handlers;

public class PackAutocompleteHandler : AutocompleteHandler
{
    private readonly HengcordTCGClient _client;

    public PackAutocompleteHandler(HengcordTCGClient client)
    {
        _client = client;
    }

    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(
        IInteractionContext context, 
        IAutocompleteInteraction interaction, 
        IParameterInfo parameter, 
        IServiceProvider services)
    {
        var userInput = (interaction.Data.Current.Value as string) ?? "";
        
        var packs = await _client.GetPacksAsync();
        
        var results = packs
            .Where(p => p.IsAvailable)
            .Where(p => p.Name.ToLower().Contains(userInput.ToLower()))
            .Take(25)
            .Select(p => new AutocompleteResult($"{p.Name} ({p.Price}g)", p.Name));

        return AutocompletionResult.FromSuccess(results);
    }
}
