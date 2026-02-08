using System.ComponentModel.DataAnnotations.Schema;

namespace HengcordTCG.Shared.Models;

public class UserCard
{
    public int Id { get; set; }

    public int UserId { get; set; }
    [ForeignKey("UserId")]
    public User User { get; set; } = null!;

    public int CardId { get; set; }
    [ForeignKey("CardId")]
    public Card Card { get; set; } = null!;

    public int Count { get; set; } = 1;
    public DateTime ObtainedAt { get; set; } = DateTime.UtcNow;
}
