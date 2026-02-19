namespace HengcordTCG.Shared.DTOs.Auth;

public class UserInfo
{
    public ulong Id { get; set; }
    public string Username { get; set; } = "";
    public string? AvatarUrl { get; set; }
    public long Gold { get; set; }
    public bool IsBotAdmin { get; set; }
    public DateTime? LastDaily { get; set; }
}

public class AuthInfo
{
    public bool IsAuthenticated { get; set; }
    public string? Name { get; set; }
    public string? AvatarUrl { get; set; }
    public bool IsAdmin { get; set; }
    public string? UserId { get; set; }
}

public class UserDetails
{
    public ulong Id { get; set; }
    public ulong DiscordId { get; set; }
    public string Username { get; set; } = "";
    public long Gold { get; set; }
    public bool IsBotAdmin { get; set; }
    public DateTime? LastDaily { get; set; }
}
