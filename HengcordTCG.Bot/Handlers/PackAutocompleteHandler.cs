using Discord;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using HengcordTCG.Shared.Data;

namespace HengcordTCG.Bot.Handlers;

public class PackAutocompleteHandler : AutocompleteHandler
{
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(
        IInteractionContext context, 
        IAutocompleteInteraction interaction, 
        IParameterInfo parameter, 
        IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        var userInput = (interaction.Data.Current.Value as string) ?? "";
        
        var packs = await db.PackTypes
            .Where(p => p.IsActive)
            .Where(p => p.Name.ToLower().Contains(userInput.ToLower()))
            .Take(25) // Max 25 suggestions allowed by Discord
            .Select(p => new { p.Name, p.Price })
            .ToListAsync();
        
        var results = packs.Select(p => new AutocompleteResult($"{p.Name} ({p.Price}g)", p.Name));

        return AutocompletionResult.FromSuccess(results);
    }
}
