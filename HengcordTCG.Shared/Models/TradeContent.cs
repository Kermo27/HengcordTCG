namespace HengcordTCG.Shared.Models;

public class TradeContent
{
    public long Gold { get; set; }
    public Dictionary<int, int> Cards { get; set; } = new(); // CardId -> Count
    public Dictionary<string, int> CardNames { get; set; } = new(); // Name -> Count
}
