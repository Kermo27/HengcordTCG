using HengcordTCG.Shared.Data;
using HengcordTCG.Shared.Models;
using HengcordTCG.Shared.Results;
using Microsoft.EntityFrameworkCore;
using AutoMapper;

namespace HengcordTCG.Server.Services;

public interface IPackService
{
    Task<Result<List<PackType>>> GetAllAsync();
    Task<Result<PackType>> GetByIdAsync(int id);
    Task<Result<PackType>> GetByNameAsync(string name);
    Task<Result<PackType>> AddAsync(PackType pack);
    Task<Result<PackType>> UpdateAsync(int id, PackType pack);
    Task<Result> DeleteAsync(int id);
    Task<Result<(string Name, bool IsAvailable)>> ToggleAvailabilityAsync(string packName);
}

public class PackService : IPackService
{
    private readonly AppDbContext _context;
    private readonly ILogger<PackService> _logger;
    private readonly IMapper _mapper;

    public PackService(AppDbContext context, ILogger<PackService> logger, IMapper mapper)
    {
        _context = context;
        _logger = logger;
        _mapper = mapper;
    }

    public async Task<Result<List<PackType>>> GetAllAsync()
    {
        try
        {
            var packs = await _context.PackTypes.ToListAsync();
            return Result<List<PackType>>.Success(packs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all packs");
            return Result<List<PackType>>.Failure("DATABASE_ERROR", "Failed to retrieve packs");
        }
    }

    public async Task<Result<PackType>> GetByIdAsync(int id)
    {
        try
        {
            var pack = await _context.PackTypes.FindAsync(id);
            if (pack == null)
                return Result<PackType>.Failure("NOT_FOUND", $"Pack with ID {id} not found");
            
            return Result<PackType>.Success(pack);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get pack by ID {PackId}", id);
            return Result<PackType>.Failure("DATABASE_ERROR", "Failed to retrieve pack");
        }
    }

    public async Task<Result<PackType>> GetByNameAsync(string name)
    {
        try
        {
            var pack = await _context.PackTypes.FirstOrDefaultAsync(p => p.Name == name);
            if (pack == null)
                return Result<PackType>.Failure("NOT_FOUND", $"Pack '{name}' not found");
            
            return Result<PackType>.Success(pack);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get pack by name {PackName}", name);
            return Result<PackType>.Failure("DATABASE_ERROR", "Failed to retrieve pack");
        }
    }

    public async Task<Result<PackType>> AddAsync(PackType pack)
    {
        try
        {
            _context.PackTypes.Add(pack);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Added new pack: {PackName} (ID: {PackId})", pack.Name, pack.Id);
            return Result<PackType>.Success(pack);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add pack: {PackName}", pack.Name);
            return Result<PackType>.Failure("DATABASE_ERROR", "Failed to add pack");
        }
    }

    public async Task<Result<PackType>> UpdateAsync(int id, PackType packUpdate)
    {
        try
        {
            var pack = await _context.PackTypes.FindAsync(id);
            if (pack == null)
            {
                _logger.LogWarning("Attempted to update non-existent pack ID: {PackId}", id);
                return Result<PackType>.Failure("NOT_FOUND", "Pack not found");
            }

            _mapper.Map(packUpdate, pack);

            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Updated pack: {PackName} (ID: {PackId})", pack.Name, pack.Id);
            return Result<PackType>.Success(pack);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update pack ID: {PackId}", id);
            return Result<PackType>.Failure("DATABASE_ERROR", "Failed to update pack");
        }
    }

    public async Task<Result> DeleteAsync(int id)
    {
        try
        {
            var pack = await _context.PackTypes.FindAsync(id);
            if (pack == null)
            {
                _logger.LogWarning("Attempted to delete non-existent pack ID: {PackId}", id);
                return Result.Failure("NOT_FOUND", "Pack not found");
            }

            _context.PackTypes.Remove(pack);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Deleted pack: {PackName} (ID: {PackId})", pack.Name, id);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete pack ID: {PackId}", id);
            return Result.Failure("DATABASE_ERROR", "Failed to delete pack");
        }
    }

    public async Task<Result<(string Name, bool IsAvailable)>> ToggleAvailabilityAsync(string packName)
    {
        try
        {
            var pack = await _context.PackTypes.FirstOrDefaultAsync(p => p.Name == packName);
            if (pack == null)
            {
                _logger.LogWarning("Attempted to toggle non-existent pack: {PackName}", packName);
                return Result<(string, bool)>.Failure("NOT_FOUND", "Pack not found");
            }

            pack.IsAvailable = !pack.IsAvailable;
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Toggled pack {PackName} availability to {IsAvailable}", packName, pack.IsAvailable);
            return Result<(string, bool)>.Success((pack.Name, pack.IsAvailable));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle pack availability: {PackName}", packName);
            return Result<(string, bool)>.Failure("DATABASE_ERROR", "Failed to toggle pack availability");
        }
    }
}
