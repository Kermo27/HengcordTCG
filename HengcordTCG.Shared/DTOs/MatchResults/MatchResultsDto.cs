namespace HengcordTCG.Shared.DTOs.MatchResults;

public record SaveMatchRequest(
    ulong WinnerDiscordId,
    ulong LoserDiscordId,
    int Turns,
    int WinnerHpRemaining
);

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

public record LeaderboardEntry(
    string Username,
    ulong DiscordId,
    int Wins,
    int Losses,
    double WinRate
);
