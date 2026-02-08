namespace HengcordTCG.Shared.Models;

public class User
{
    public int Id { get; set; }
    public ulong DiscordId { get; set; }
    public string Username { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
    
    public long Gold { get; set; } = 0;
    public bool IsBotAdmin { get; set; } = false;
    public DateTime? LastDaily { get; set; }
    
    public List<UserCard> UserCards { get; set; } = new();
}
