using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using HengcordTCG.Shared.DTOs.MatchResults;
using HengcordTCG.Server.Services;

namespace HengcordTCG.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MatchResultsController : ControllerBase
{
    private readonly IMatchService _matchService;
    private readonly ILogger<MatchResultsController> _logger;

    public MatchResultsController(IMatchService matchService, ILogger<MatchResultsController> logger)
    {
        _matchService = matchService;
        _logger = logger;
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult> SaveMatch([FromBody] SaveMatchRequest request)
    {
        var result = await _matchService.SaveMatchAsync(request);
        if (result.IsFailure)
        {
            return result.Error.Code == "USER_NOT_FOUND" 
                ? NotFound(result.Error.Message) 
                : BadRequest(result.Error.Message);
        }
        return Ok(new { id = result.Value });
    }

    [HttpGet("stats/{discordId}")]
    [AllowAnonymous]
    public async Task<ActionResult> GetPlayerStats(ulong discordId)
    {
        var result = await _matchService.GetPlayerStatsAsync(discordId);
        if (result.IsFailure)
        {
            return result.Error.Code == "USER_NOT_FOUND" 
                ? NotFound(result.Error.Message) 
                : BadRequest(result.Error.Message);
        }
        return Ok(result.Value);
    }

    [HttpGet("leaderboard")]
    [AllowAnonymous]
    public async Task<ActionResult> GetLeaderboard([FromQuery] int top = 10)
    {
        var result = await _matchService.GetLeaderboardAsync(top);
        if (result.IsFailure)
        {
            return BadRequest(result.Error.Message);
        }
        return Ok(result.Value);
    }
}
