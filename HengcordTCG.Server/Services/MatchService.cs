using HengcordTCG.Shared.Data;
using HengcordTCG.Shared.Models;
using HengcordTCG.Shared.Results;
using HengcordTCG.Shared.DTOs.MatchResults;
using Microsoft.EntityFrameworkCore;

namespace HengcordTCG.Server.Services;

public interface IMatchService
{
    Task<Result<int>> SaveMatchAsync(SaveMatchRequest request);
    Task<Result<PlayerStatsResponse>> GetPlayerStatsAsync(ulong discordId);
    Task<Result<List<LeaderboardEntry>>> GetLeaderboardAsync(int top = 10);
}

public class MatchService : IMatchService
{
    private readonly AppDbContext _context;
    private readonly ILogger<MatchService> _logger;

    public MatchService(AppDbContext context, ILogger<MatchService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<int>> SaveMatchAsync(SaveMatchRequest request)
    {
        try
        {
            var winner = await _context.Users.FirstOrDefaultAsync(u => u.DiscordId == request.WinnerDiscordId);
            var loser = await _context.Users.FirstOrDefaultAsync(u => u.DiscordId == request.LoserDiscordId);

            if (winner == null || loser == null)
            {
                _logger.LogWarning("Attempted to save match with non-existent users: Winner={WinnerId}, Loser={LoserId}", 
                    request.WinnerDiscordId, request.LoserDiscordId);
                return Result<int>.Failure("USER_NOT_FOUND", "Winner or loser not found");
            }

            var result = new MatchResult
            {
                WinnerId = winner.Id,
                LoserId = loser.Id,
                Turns = request.Turns,
                WinnerHpRemaining = request.WinnerHpRemaining,
                FinishedAt = DateTime.UtcNow
            };

            _context.MatchResults.Add(result);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Saved match result: Winner={WinnerId}, Loser={LoserId}, MatchId={MatchId}", 
                winner.DiscordId, loser.DiscordId, result.Id);
            
            return Result<int>.Success(result.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save match result");
            return Result<int>.Failure("DATABASE_ERROR", "Failed to save match result");
        }
    }

    public async Task<Result<PlayerStatsResponse>> GetPlayerStatsAsync(ulong discordId)
    {
        try
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.DiscordId == discordId);
            if (user == null)
                return Result<PlayerStatsResponse>.Failure("USER_NOT_FOUND", "User not found");

            var wins = await _context.MatchResults
                .Where(m => m.WinnerId == user.Id)
                .ToListAsync();

            var losses = await _context.MatchResults
                .Where(m => m.LoserId == user.Id)
                .ToListAsync();

            var totalGames = wins.Count + losses.Count;
            var winRate = totalGames > 0 ? (double)wins.Count / totalGames * 100 : 0;

            var recentMatches = await _context.MatchResults
                .Where(m => m.WinnerId == user.Id || m.LoserId == user.Id)
                .OrderByDescending(m => m.FinishedAt)
                .Take(10)
                .Include(m => m.Winner)
                .Include(m => m.Loser)
                .ToListAsync();

            var recentInfo = recentMatches.Select(m =>
            {
                var won = m.WinnerId == user.Id;
                var opponent = won ? m.Loser : m.Winner;
                return new RecentMatchInfo(
                    opponent.Username,
                    won,
                    m.Turns,
                    m.WinnerHpRemaining,
                    m.FinishedAt
                );
            }).ToList();

            var stats = new PlayerStatsResponse(
                wins.Count,
                losses.Count,
                totalGames,
                Math.Round(winRate, 1),
                wins.Any() ? wins.Max(w => w.WinnerHpRemaining) : 0,
                wins.Any() ? wins.Min(w => w.Turns) : 0,
                recentInfo
            );

            return Result<PlayerStatsResponse>.Success(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get player stats for {DiscordId}", discordId);
            return Result<PlayerStatsResponse>.Failure("DATABASE_ERROR", "Failed to retrieve player stats");
        }
    }

    public async Task<Result<List<LeaderboardEntry>>> GetLeaderboardAsync(int top = 10)
    {
        try
        {
            var allMatches = await _context.MatchResults
                .Include(m => m.Winner)
                .Include(m => m.Loser)
                .ToListAsync();

            var playerStats = new Dictionary<int, (User User, int Wins, int Losses)>();

            foreach (var match in allMatches)
            {
                if (!playerStats.ContainsKey(match.WinnerId))
                    playerStats[match.WinnerId] = (match.Winner, 0, 0);
                if (!playerStats.ContainsKey(match.LoserId))
                    playerStats[match.LoserId] = (match.Loser, 0, 0);

                var winnerStats = playerStats[match.WinnerId];
                playerStats[match.WinnerId] = (winnerStats.User, winnerStats.Wins + 1, winnerStats.Losses);

                var loserStats = playerStats[match.LoserId];
                playerStats[match.LoserId] = (loserStats.User, loserStats.Wins, loserStats.Losses + 1);
            }

            var leaderboard = playerStats.Values
                .OrderByDescending(p => p.Wins)
                .ThenBy(p => p.Losses)
                .Take(top)
                .Select(p => new LeaderboardEntry(
                    p.User.Username,
                    p.User.DiscordId,
                    p.Wins,
                    p.Losses,
                    (p.Wins + p.Losses) > 0 ? Math.Round((double)p.Wins / (p.Wins + p.Losses) * 100, 1) : 0
                ))
                .ToList();

            return Result<List<LeaderboardEntry>>.Success(leaderboard);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get leaderboard");
            return Result<List<LeaderboardEntry>>.Failure("DATABASE_ERROR", "Failed to retrieve leaderboard");
        }
    }
}
