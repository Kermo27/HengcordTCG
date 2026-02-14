using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace HengcordTCG.Server.Extensions;

public static class ControllerBaseExtensions
{
    public static bool TryGetDiscordId(this ControllerBase controller, out ulong discordId)
    {
        discordId = 0;
        var discordIdStr = controller.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return ulong.TryParse(discordIdStr, out discordId);
    }
}
