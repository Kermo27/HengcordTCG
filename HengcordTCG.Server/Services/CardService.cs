using HengcordTCG.Shared.Data;
using HengcordTCG.Shared.Models;
using HengcordTCG.Shared.Results;
using Microsoft.EntityFrameworkCore;
using AutoMapper;

namespace HengcordTCG.Server.Services;

public interface ICardService
{
    Task<Result<List<Card>>> GetAllAsync();
    Task<Result<Card>> GetByIdAsync(int id);
    Task<Result<Card>> GetByNameAsync(string name);
    Task<Result<Card>> AddAsync(Card card);
    Task<Result<Card>> UpdateAsync(int id, Card card);
    Task<Result> DeleteAsync(string name);
    Task<Result> SetCardPackAsync(string cardName, string packName);
}

public class CardService : ICardService
{
    private readonly AppDbContext _context;
    private readonly ILogger<CardService> _logger;
    private readonly IMapper _mapper;

    public CardService(AppDbContext context, ILogger<CardService> logger, IMapper mapper)
    {
        _context = context;
        _logger = logger;
        _mapper = mapper;
    }

    public async Task<Result<List<Card>>> GetAllAsync()
    {
        try
        {
            var cards = await _context.Cards.ToListAsync();
            return Result<List<Card>>.Success(cards);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all cards");
            return Result<List<Card>>.Failure("DATABASE_ERROR", "Failed to retrieve cards");
        }
    }

    public async Task<Result<Card>> GetByIdAsync(int id)
    {
        try
        {
            var card = await _context.Cards.FindAsync(id);
            if (card == null)
                return Result<Card>.Failure("NOT_FOUND", $"Card with ID {id} not found");
            
            return Result<Card>.Success(card);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get card by ID {CardId}", id);
            return Result<Card>.Failure("DATABASE_ERROR", "Failed to retrieve card");
        }
    }

    public async Task<Result<Card>> GetByNameAsync(string name)
    {
        try
        {
            var card = await _context.Cards.FirstOrDefaultAsync(c => c.Name == name);
            if (card == null)
                return Result<Card>.Failure("NOT_FOUND", $"Card '{name}' not found");
            
            return Result<Card>.Success(card);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get card by name {CardName}", name);
            return Result<Card>.Failure("DATABASE_ERROR", "Failed to retrieve card");
        }
    }

    public async Task<Result<Card>> AddAsync(Card card)
    {
        try
        {
            var existing = await _context.Cards.FirstOrDefaultAsync(c => c.Name == card.Name);
            if (existing != null)
            {
                _logger.LogWarning("Attempted to add duplicate card: {CardName}", card.Name);
                return Result<Card>.Failure("DUPLICATE", "Card already exists");
            }

            _context.Cards.Add(card);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Added new card: {CardName} (ID: {CardId})", card.Name, card.Id);
            return Result<Card>.Success(card);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add card: {CardName}", card.Name);
            return Result<Card>.Failure("DATABASE_ERROR", "Failed to add card");
        }
    }

    public async Task<Result<Card>> UpdateAsync(int id, Card card)
    {
        try
        {
            var existing = await _context.Cards.FindAsync(id);
            if (existing == null)
            {
                _logger.LogWarning("Attempted to update non-existent card ID: {CardId}", id);
                return Result<Card>.Failure("NOT_FOUND", $"Card with ID {id} not found");
            }

            _mapper.Map(card, existing);

            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Updated card: {CardName} (ID: {CardId})", existing.Name, existing.Id);
            return Result<Card>.Success(existing);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update card ID: {CardId}", id);
            return Result<Card>.Failure("DATABASE_ERROR", "Failed to update card");
        }
    }

    public async Task<Result> DeleteAsync(string name)
    {
        try
        {
            var card = await _context.Cards.FirstOrDefaultAsync(c => c.Name == name);
            if (card == null)
            {
                _logger.LogWarning("Attempted to delete non-existent card: {CardName}", name);
                return Result.Failure("NOT_FOUND", "Card not found");
            }

            _context.Cards.Remove(card);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Deleted card: {CardName}", name);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete card: {CardName}", name);
            return Result.Failure("DATABASE_ERROR", "Failed to delete card");
        }
    }

    public async Task<Result> SetCardPackAsync(string cardName, string packName)
    {
        try
        {
            var card = await _context.Cards.FirstOrDefaultAsync(c => c.Name == cardName);
            if (card == null)
            {
                return Result.Failure("CARD_NOT_FOUND", "Card does not exist");
            }

            if (packName.ToLower() == "null")
            {
                card.ExclusivePackId = null;
            }
            else
            {
                var pack = await _context.PackTypes.FirstOrDefaultAsync(p => p.Name == packName);
                if (pack == null)
                {
                    return Result.Failure("PACK_NOT_FOUND", "Pack does not exist");
                }
                card.ExclusivePackId = pack.Id;
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Set card {CardName} pack to {PackName}", cardName, packName);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set card pack for {CardName}", cardName);
            return Result.Failure("DATABASE_ERROR", "Failed to set card pack");
        }
    }
}
