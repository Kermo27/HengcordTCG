using Discord;
using Discord.Interactions;
using HengcordTCG.Shared.Clients;

namespace HengcordTCG.Bot.Handlers;

public class CardAutocompleteHandler : AutocompleteHandler
{
    private readonly HengcordTCGClient _client;

    public CardAutocompleteHandler(HengcordTCGClient client)
    {
        _client = client;
    }

    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(
        IInteractionContext context, 
        IAutocompleteInteraction interaction, 
        IParameterInfo parameter, 
        IServiceProvider services)
    {
        var userInput = (interaction.Data.Current.Value as string)?.ToLower() ?? "";
        
        var cards = await _client.GetCardsAsync();
        var suggestions = cards
            .Where(c => c.Name.ToLower().Contains(userInput))
            .OrderBy(c => c.Name)
            .Take(25)
            .Select(c => new AutocompleteResult(c.Name, c.Name))
            .ToList();
        return AutocompletionResult.FromSuccess(suggestions);
    }
}
