using Discord;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;
using HengcordTCG.Shared.Clients;

namespace HengcordTCG.Bot.Handlers;

public class PackAutocompleteHandler : AutocompleteHandler
{
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(
        IInteractionContext context, 
        IAutocompleteInteraction interaction, 
        IParameterInfo parameter, 
        IServiceProvider services)
    {
        var client = services.GetRequiredService<HengcordTCGClient>();
        
        var userInput = (interaction.Data.Current.Value as string) ?? "";
        
        var packs = await client.GetPacksAsync();
        
        var results = packs
            .Where(p => p.IsActive)
            .Where(p => p.Name.ToLower().Contains(userInput.ToLower()))
            .Take(25)
            .Select(p => new AutocompleteResult($"{p.Name} ({p.Price}g)", p.Name));

        return AutocompletionResult.FromSuccess(results);
    }
}
