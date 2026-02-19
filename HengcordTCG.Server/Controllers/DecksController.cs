using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using HengcordTCG.Shared.DTOs.Decks;
using HengcordTCG.Server.Services;

namespace HengcordTCG.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DecksController : ControllerBase
{
    private readonly IDeckService _deckService;
    private readonly ILogger<DecksController> _logger;

    public DecksController(IDeckService deckService, ILogger<DecksController> logger)
    {
        _deckService = deckService;
        _logger = logger;
    }

    [HttpGet("{discordId}")]
    public async Task<ActionResult> GetDeck(ulong discordId)
    {
        var result = await _deckService.GetByDiscordIdAsync(discordId);
        if (result.IsFailure)
        {
            return result.Error.Code == "USER_NOT_FOUND" ? NotFound(result.Error.Message) : BadRequest(result.Error.Message);
        }
        return Ok(result.Value);
    }

    [HttpPost("save")]
    public async Task<ActionResult> SaveDeck([FromBody] SaveDeckRequest request)
    {
        var result = await _deckService.SaveAsync(request);
        if (result.IsFailure)
        {
            _logger.LogWarning("Failed to save deck for user {DiscordId}: {Error}", request.DiscordId, result.Error.Message);
            return result.Error.Code == "USER_NOT_FOUND" 
                ? NotFound(result.Error.Message) 
                : BadRequest(result.Error.Message);
        }
        return Ok(new { message = "Deck saved successfully!" });
    }
}
