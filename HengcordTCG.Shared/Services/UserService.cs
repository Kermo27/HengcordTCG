using Microsoft.EntityFrameworkCore;
using HengcordTCG.Shared.Data;
using Microsoft.Extensions.Logging;

namespace HengcordTCG.Shared.Services;

public class UserService
{
    private readonly AppDbContext _db;
    private readonly ILogger<UserService> _logger;
    private static readonly Random _random = new Random();
    private static readonly object _randomLock = new object();

    public UserService(AppDbContext db, ILogger<UserService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Models.User?> GetByDiscordIdAsync(ulong discordId)
    {
        return await _db.Users.FirstOrDefaultAsync(u => u.DiscordId == discordId);
    }

    public async Task<Models.User> GetOrCreateUserAsync(ulong discordId, string username)
    {
        try
        {
            var user = await GetByDiscordIdAsync(discordId);
            
            if (user == null)
            {
                _logger.LogInformation("Creating new user for Discord ID {DiscordId} with username {Username}", discordId, username);
                user = new Models.User
                {
                    DiscordId = discordId,
                    Username = username,
                    CreatedAt = DateTime.UtcNow,
                    LastSeen = DateTime.UtcNow
                };
                _db.Users.Add(user);
                await _db.SaveChangesAsync();
                _logger.LogInformation("User created successfully for Discord ID {DiscordId}", discordId);
            }
            else
            {
                user.LastSeen = DateTime.UtcNow;
                user.Username = username;
                await _db.SaveChangesAsync();
                _logger.LogDebug("User updated for Discord ID {DiscordId}", discordId);
            }

            return user;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating/updating user for Discord ID {DiscordId}", discordId);
            throw;
        }
    }

    public async Task AddGoldAsync(ulong discordId, long amount)
    {
        var user = await GetByDiscordIdAsync(discordId);
        if (user != null)
        {
            if (user.Gold + amount < 0)
            {
                throw new InvalidOperationException($"Insufficient gold. Current: {user.Gold}, Attempting to subtract: {-amount}");
            }
            
            user.Gold += amount;
            await _db.SaveChangesAsync();
            _logger.LogInformation("Gold updated for Discord ID {DiscordId}. Amount: {Amount}, New Balance: {NewBalance}", discordId, amount, user.Gold);
        }
    }

    public async Task<(bool success, long amount, TimeSpan? timeRemaining)> ClaimDailyAsync(ulong discordId, string username)
    {
        try
        {
            var user = await GetOrCreateUserAsync(discordId, username);
            var now = DateTime.UtcNow;

            // Use lock to prevent race condition
            lock (_randomLock)
            {
                if (user.LastDaily.HasValue)
                {
                    var diff = now - user.LastDaily.Value;
                    if (diff.TotalHours < 20)
                    {
                        var timeRemaining = TimeSpan.FromHours(20) - diff;
                        _logger.LogInformation("Daily cooldown active for Discord ID {DiscordId}. Time remaining: {TimeRemaining}", discordId, timeRemaining);
                        return (false, 0, timeRemaining);
                    }
                }

                var amount = _random.Next(100, 501);
                user.Gold += amount;
                user.LastDaily = now;

                // Save immediately within the lock to prevent race condition
                _db.SaveChanges();
                _logger.LogInformation("Daily claimed for Discord ID {DiscordId}. Amount: {Amount} gold", discordId, amount);

                return (true, amount, null);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error claiming daily for Discord ID {DiscordId}", discordId);
            throw;
        }
    }
}
