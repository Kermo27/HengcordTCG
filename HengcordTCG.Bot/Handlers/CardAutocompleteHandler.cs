using Discord;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore;
using HengcordTCG.Shared.Data;

namespace HengcordTCG.Bot.Handlers;

public class CardAutocompleteHandler : AutocompleteHandler
{
    private readonly AppDbContext _db;

    public CardAutocompleteHandler(AppDbContext db)
    {
        _db = db;
    }

    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(
        IInteractionContext context, 
        IAutocompleteInteraction interaction, 
        IParameterInfo parameter, 
        IServiceProvider services)
    {
        var userInput = (interaction.Data.Current.Value as string)?.ToLower() ?? "";
        
        var cards = await _db.Cards
            .Where(c => c.Name.ToLower().Contains(userInput))
            .OrderBy(c => c.Name)
            .Take(25) // Discord limit is 25
            .ToListAsync();
        
        var suggestions = cards
            .Select(c => new AutocompleteResult(c.Name, c.Name))
            .ToList();

        return AutocompletionResult.FromSuccess(suggestions);
    }
}
