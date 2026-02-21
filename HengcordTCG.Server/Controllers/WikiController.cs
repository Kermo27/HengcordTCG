using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HengcordTCG.Shared.DTOs.Wiki;
using HengcordTCG.Shared.Services;
using System.Security.Claims;
using HengcordTCG.Server.Authentication;

namespace HengcordTCG.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WikiController : ControllerBase
{
    private readonly WikiService _wikiService;
    private readonly WikiProposalService _proposalService;

    public WikiController(WikiService wikiService, WikiProposalService proposalService)
    {
        _wikiService = wikiService;
        _proposalService = proposalService;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<List<WikiPageDto>>> GetPages()
    {
        var pages = await _wikiService.GetPagesAsync();
        return Ok(pages);
    }

    [HttpGet("tree")]
    [AllowAnonymous]
    public async Task<ActionResult<List<WikiPageTreeDto>>> GetTree()
    {
        var tree = await _wikiService.GetTreeAsync();
        return Ok(tree);
    }

    [HttpGet("{slug}")]
    [AllowAnonymous]
    public async Task<ActionResult<WikiPageDetailDto>> GetPage(string slug)
    {
        var page = await _wikiService.GetPageBySlugAsync(slug);
        if (page == null)
            return NotFound(new { message = "Page not found" });
        return Ok(page);
    }

    [HttpGet("search")]
    [AllowAnonymous]
    public async Task<ActionResult<List<WikiPageDto>>> Search([FromQuery] string q)
    {
        var results = await _wikiService.SearchAsync(q);
        return Ok(results);
    }

    [HttpPost]
    [Authorize(AuthenticationSchemes = $"{JwtBearerDefaults.AuthenticationScheme},{ApiKeyAuthenticationOptions.DefaultScheme}", Roles = "Admin")]
    public async Task<ActionResult<WikiPageDto>> CreatePage([FromBody] CreateWikiPageRequest request)
    {
        var discordId = User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(discordId) || !ulong.TryParse(discordId, out var editorId))
            return Unauthorized();

        var (page, error) = await _wikiService.CreatePageAsync(request, editorId);
        if (error != null)
            return BadRequest(new { message = error });
        
        return Ok(page);
    }

    [HttpPut("{id}")]
    [Authorize(AuthenticationSchemes = $"{JwtBearerDefaults.AuthenticationScheme},{ApiKeyAuthenticationOptions.DefaultScheme}", Roles = "Admin")]
    public async Task<ActionResult<WikiPageDto>> UpdatePage(int id, [FromBody] UpdateWikiPageRequest request)
    {
        var discordId = User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(discordId) || !ulong.TryParse(discordId, out var editorId))
            return Unauthorized();

        var (page, error) = await _wikiService.UpdatePageAsync(id, request, editorId);
        if (error != null)
            return BadRequest(new { message = error });
        
        return Ok(page);
    }

    [HttpDelete("{id}")]
    [Authorize(AuthenticationSchemes = $"{JwtBearerDefaults.AuthenticationScheme},{ApiKeyAuthenticationOptions.DefaultScheme}", Roles = "Admin")]
    public async Task<ActionResult> DeletePage(int id)
    {
        var deleted = await _wikiService.DeletePageAsync(id);
        if (!deleted)
            return NotFound();
        return Ok();
    }

    [HttpGet("{id}/history")]
    [AllowAnonymous]
    public async Task<ActionResult<List<WikiHistoryDto>>> GetPageHistory(int id)
    {
        var history = await _wikiService.GetPageHistoryAsync(id);
        return Ok(history);
    }

    [HttpPost("proposals")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ActionResult<WikiProposalDto>> CreateProposal([FromBody] CreateProposalRequest request)
    {
        var discordId = User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(discordId) || !ulong.TryParse(discordId, out var userId))
            return Unauthorized();

        var proposal = await _proposalService.CreateProposalAsync(request, userId);
        return Ok(proposal);
    }

    [HttpGet("proposals")]
    [Authorize(AuthenticationSchemes = $"{JwtBearerDefaults.AuthenticationScheme},{ApiKeyAuthenticationOptions.DefaultScheme}", Roles = "Admin")]
    public async Task<ActionResult<List<WikiProposalListDto>>> GetProposals()
    {
        var proposals = await _proposalService.GetPendingProposalsAsync();
        return Ok(proposals);
    }

    [HttpGet("proposals/{id}")]
    [Authorize(AuthenticationSchemes = $"{JwtBearerDefaults.AuthenticationScheme},{ApiKeyAuthenticationOptions.DefaultScheme}", Roles = "Admin")]
    public async Task<ActionResult<WikiProposalDetailDto>> GetProposal(int id)
    {
        var proposal = await _proposalService.GetProposalAsync(id);
        if (proposal == null)
            return NotFound();
        return Ok(proposal);
    }

    [HttpPost("proposals/{id}/approve")]
    [Authorize(AuthenticationSchemes = $"{JwtBearerDefaults.AuthenticationScheme},{ApiKeyAuthenticationOptions.DefaultScheme}", Roles = "Admin")]
    public async Task<ActionResult> ApproveProposal(int id)
    {
        var discordId = User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(discordId) || !ulong.TryParse(discordId, out var adminId))
            return Unauthorized();

        var (success, error) = await _proposalService.ApproveProposalAsync(id, adminId);
        if (!success)
            return NotFound(new { message = error });
        return Ok(new { message = "Propozycja zatwierdzona" });
    }

    [HttpPost("proposals/{id}/reject")]
    [Authorize(AuthenticationSchemes = $"{JwtBearerDefaults.AuthenticationScheme},{ApiKeyAuthenticationOptions.DefaultScheme}", Roles = "Admin")]
    public async Task<ActionResult> RejectProposal(int id, [FromBody] RejectProposalRequest request)
    {
        var discordId = User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(discordId) || !ulong.TryParse(discordId, out var adminId))
            return Unauthorized();

        var (success, error) = await _proposalService.RejectProposalAsync(id, adminId, request.Reason);
        if (!success)
            return NotFound(new { message = error });
        return Ok(new { message = "Propozycja odrzucona" });
    }
}
