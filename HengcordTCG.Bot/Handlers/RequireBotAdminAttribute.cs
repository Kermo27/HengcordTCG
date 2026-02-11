using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using HengcordTCG.Shared.Clients;

namespace HengcordTCG.Bot.Handlers;

public class RequireBotAdminAttribute : PreconditionAttribute
{
    public override async Task<PreconditionResult> CheckRequirementsAsync(
        IInteractionContext context,
        ICommandInfo commandInfo,
        IServiceProvider services)
    {
        var config = services.GetRequiredService<IConfiguration>();
        var client = services.GetRequiredService<HengcordTCGClient>();
        
        var adminIds = config.GetSection("BotAdmins").Get<ulong[]>() ?? [];
        if (adminIds.Contains(context.User.Id))
        {
            return PreconditionResult.FromSuccess();
        }
        
        var user = await client.GetUserAsync(context.User.Id);

        if (user != null && user.IsBotAdmin)
        {
            return PreconditionResult.FromSuccess();
        }

        return PreconditionResult.FromError("You don't have bot admin privileges.");
    }
}
