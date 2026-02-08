using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using HengcordTCG.Shared.Data;

namespace HengcordTCG.Bot.Handlers;

public class RequireBotAdminAttribute : PreconditionAttribute
{
    public override async Task<PreconditionResult> CheckRequirementsAsync(
        IInteractionContext context,
        ICommandInfo commandInfo,
        IServiceProvider services)
    {
        var config = services.GetRequiredService<IConfiguration>();
        var db = services.GetRequiredService<AppDbContext>();
        
        var adminIds = config.GetSection("BotAdmins").Get<ulong[]>() ?? [];
        if (adminIds.Contains(context.User.Id))
        {
            return PreconditionResult.FromSuccess();
        }
        
        var isDbAdmin = await db.Users
            .AnyAsync(u => u.DiscordId == context.User.Id && u.IsBotAdmin);

        if (isDbAdmin)
        {
            return PreconditionResult.FromSuccess();
        }

        return PreconditionResult.FromError("Nie masz uprawnień admina bota.");
    }
}
