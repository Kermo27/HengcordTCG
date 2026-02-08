using Microsoft.EntityFrameworkCore;
using HengcordTCG.Shared.Data;

namespace HengcordTCG.Shared.Services;

public class UserService
{
    private readonly AppDbContext _db;

    public UserService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Models.User?> GetByDiscordIdAsync(ulong discordId)
    {
        return await _db.Users.FirstOrDefaultAsync(u => u.DiscordId == discordId);
    }

    public async Task<Models.User> GetOrCreateUserAsync(ulong discordId, string username)
    {
        var user = await GetByDiscordIdAsync(discordId);
        
        if (user == null)
        {
            user = new Models.User
            {
                DiscordId = discordId,
                Username = username,
                CreatedAt = DateTime.UtcNow,
                LastSeen = DateTime.UtcNow
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
        }
        else
        {
            user.LastSeen = DateTime.UtcNow;
            user.Username = username;
            await _db.SaveChangesAsync();
        }

        return user;
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
        var user = await GetOrCreateUserAsync(discordId, username);
        var now = DateTime.UtcNow;

        if (user.LastDaily.HasValue)
        {
            var diff = now - user.LastDaily.Value;
            if (diff.TotalHours < 20)
            {
                return (false, 0, TimeSpan.FromHours(20) - diff);
            }
        }

        var amount = new Random().Next(100, 501);
        user.Gold += amount;
        user.LastDaily = now;
        
        await _db.SaveChangesAsync();
        
        return (true, amount, null);
    }
}
