using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HengcordTCG.Shared.Data;
using HengcordTCG.Shared.Models;

namespace HengcordTCG.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MatchResultsController : ControllerBase
{
    private readonly AppDbContext _context;

    public MatchResultsController(AppDbContext context)
    {
        _context = context;
    }

    public record SaveMatchRequest(
        ulong WinnerDiscordId,
        ulong LoserDiscordId,
        int Turns,
        int WinnerHpRemaining
    );

    [HttpPost]
    public async Task<ActionResult> SaveMatch([FromBody] SaveMatchRequest request)
    {
        var winner = await _context.Users.FirstOrDefaultAsync(u => u.DiscordId == request.WinnerDiscordId);
        var loser = await _context.Users.FirstOrDefaultAsync(u => u.DiscordId == request.LoserDiscordId);

        if (winner == null || loser == null)
            return BadRequest("Winner or loser not found");

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
        return Ok(new { id = result.Id });
    }

    public record PlayerStatsResponse(
        int Wins,
        int Losses,
        int TotalGames,
        double WinRate,
        int BestHpRemaining,
        int ShortestWin,
        List<RecentMatchInfo> RecentMatches
    );

    public record RecentMatchInfo(
        string OpponentName,
        bool Won,
        int Turns,
        int HpRemaining,
        DateTime FinishedAt
    );

    [HttpGet("stats/{discordId}")]
    public async Task<ActionResult<PlayerStatsResponse>> GetPlayerStats(ulong discordId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.DiscordId == discordId);
        if (user == null) return NotFound("User not found");

        var wins = await _context.MatchResults
            .Where(m => m.WinnerId == user.Id)
            .ToListAsync();

        var losses = await _context.MatchResults
            .Where(m => m.LoserId == user.Id)
            .ToListAsync();

        var totalGames = wins.Count + losses.Count;
        var winRate = totalGames > 0 ? (double)wins.Count / totalGames * 100 : 0;

        // Recent matches (last 10)
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

        return Ok(new PlayerStatsResponse(
            wins.Count,
            losses.Count,
            totalGames,
            Math.Round(winRate, 1),
            wins.Any() ? wins.Max(w => w.WinnerHpRemaining) : 0,
            wins.Any() ? wins.Min(w => w.Turns) : 0,
            recentInfo
        ));
    }

    public record LeaderboardEntry(
        string Username,
        ulong DiscordId,
        int Wins,
        int Losses,
        double WinRate
    );

    [HttpGet("leaderboard")]
    public async Task<ActionResult<List<LeaderboardEntry>>> GetLeaderboard([FromQuery] int top = 10)
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

        return Ok(leaderboard);
    }
}
