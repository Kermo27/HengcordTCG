using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HengcordTCG.Shared.Data;
using HengcordTCG.Shared.Models;
using HengcordTCG.Shared.Services;

namespace HengcordTCG.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TradesController : ControllerBase
{
    private readonly TradeService _tradeService;
    private readonly AppDbContext _context;

    public TradesController(TradeService tradeService, AppDbContext context)
    {
        _tradeService = tradeService;
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Trade>>> GetActiveTrades()
    {
        return await _context.Trades
            .Where(t => t.Status == TradeStatus.Pending)
            .Include(t => t.Initiator)
            .ToListAsync();
    }

    public record CreateTradeRequest(
        ulong InitiatorId, string InitiatorName,
        ulong TargetId, string TargetName,
        string Offer, string Request
    );

    [HttpPost("create")]
    public async Task<ActionResult> CreateTrade([FromBody] CreateTradeRequest req)
    {
        var result = await _tradeService.CreateTradeAsync(
            req.InitiatorId, req.InitiatorName, 
            req.TargetId, req.TargetName, 
            req.Offer, req.Request);
            
        if (!result.success) return BadRequest(result.message);
        
        return Ok(new { 
            result.success, 
            result.message, 
            result.trade, 
            result.offerContent, 
            result.requestContent 
        });
    }

    [HttpPost("{id}/accept")]
    public async Task<ActionResult> AcceptTrade(int id, [FromQuery] ulong userId)
    {
        var result = await _tradeService.AcceptTradeAsync(id, userId);
        if (!result.success) return BadRequest(result.message);
        return Ok(result.message);
    }

    [HttpPost("{id}/reject")]
    public async Task<ActionResult> RejectTrade(int id, [FromQuery] ulong userId)
    {
        var result = await _tradeService.RejectTradeAsync(id, userId);
        if (!result.success) return BadRequest(result.message);
        return Ok(result.message);
    }
}
