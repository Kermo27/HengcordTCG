using Microsoft.EntityFrameworkCore;
using HengcordTCG.Shared.Data;
using Microsoft.Extensions.Logging;

namespace HengcordTCG.Shared.Services;

public class UserService
{
    private readonly AppDbContext _db;
    private readonly ILogger<UserService> _logger;

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
                // Guard: never save a Discord ID as username
                var safeUsername = LooksLikeDiscordId(username) ? $"User_{discordId}" : username;
                _logger.LogInformation("Creating new user for Discord ID {DiscordId} with username {Username}", discordId, safeUsername);
                user = new Models.User
                {
                    DiscordId = discordId,
                    Username = safeUsername,
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
                
                if (!LooksLikeDiscordId(username))
                {
                    // Valid username provided — always update (also self-heals corrupted records)
                    if (LooksLikeDiscordId(user.Username))
                    {
                        _logger.LogWarning("Self-healing corrupted username for Discord ID {DiscordId}: '{OldUsername}' → '{NewUsername}'", 
                            discordId, user.Username, username);
                    }
                    user.Username = username;
                }
                else
                {
                    _logger.LogDebug("Skipping username update for Discord ID {DiscordId} — provided value looks like a Discord ID", discordId);
                }
                
                await _db.SaveChangesAsync();
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
            user.Gold += amount;
            await _db.SaveChangesAsync();
        }
    }

    public async Task<(bool success, long amount, TimeSpan? timeRemaining)> ClaimDailyAsync(ulong discordId, string username)
    {
        try
        {
            var user = await GetOrCreateUserAsync(discordId, username);
            var now = DateTime.UtcNow;

            if (user.LastDaily.HasValue)
            {
                var diff = now - user.LastDaily.Value;
                if (diff.TotalHours < GameConstants.DailyCooldownHours)
                {
                    var timeRemaining = TimeSpan.FromHours(GameConstants.DailyCooldownHours) - diff;
                    _logger.LogInformation("Daily cooldown active for Discord ID {DiscordId}. Time remaining: {TimeRemaining}", discordId, timeRemaining);
                    return (false, 0, timeRemaining);
                }
            }

            var amount = Random.Shared.Next(100, 501);
            user.Gold += amount;
            user.LastDaily = now;
            
            await _db.SaveChangesAsync();
            _logger.LogInformation("Daily claimed for Discord ID {DiscordId}. Amount: {Amount} gold", discordId, amount);
            
            return (true, amount, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error claiming daily for Discord ID {DiscordId}", discordId);
            throw;
        }
    }

    /// <summary>
    /// Checks if a string looks like a Discord snowflake ID (17-20 digit number).
    /// Used to prevent accidentally saving a Discord ID as a username.
    /// </summary>
    private static bool LooksLikeDiscordId(string value)
    {
        return !string.IsNullOrEmpty(value) 
            && value.Length >= 17 
            && value.Length <= 20 
            && value.All(char.IsDigit);
    }
}
