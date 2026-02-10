using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HengcordTCG.Shared.Data;
using HengcordTCG.Shared.Models;

namespace HengcordTCG.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CardsController : ControllerBase
{
    private readonly AppDbContext _context;

    public CardsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Card>>> GetCards()
    {
        return await _context.Cards.ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Card>> GetCard(int id)
    {
        var card = await _context.Cards.FindAsync(id);

        if (card == null)
        {
            return NotFound();
        }

        return card;
    }
}
