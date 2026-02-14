using System.ComponentModel.DataAnnotations.Schema;

namespace HengcordTCG.Shared.Models;

public class MatchResult
{
    public int Id { get; set; }

    public int WinnerId { get; set; }
    [ForeignKey("WinnerId")]
    public User Winner { get; set; } = null!;

    public int LoserId { get; set; }
    [ForeignKey("LoserId")]
    public User Loser { get; set; } = null!;

    public int Turns { get; set; }
    public int WinnerHpRemaining { get; set; }
    public DateTime FinishedAt { get; set; } = DateTime.UtcNow;
}
