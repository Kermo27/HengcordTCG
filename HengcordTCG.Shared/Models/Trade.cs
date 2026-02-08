using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HengcordTCG.Shared.Models;

public enum TradeStatus
{
    Pending = 0,
    Accepted = 1,
    Rejected = 2,
    Cancelled = 3,
    Executed = 4
}

public class Trade
{
    public int Id { get; set; }

    public int InitiatorId { get; set; }
    [ForeignKey("InitiatorId")]
    public User Initiator { get; set; } = null!;

    public int TargetId { get; set; }
    [ForeignKey("TargetId")]
    public User Target { get; set; } = null!;

    public long OfferGold { get; set; }
    public long RequestGold { get; set; }
    
    public string OfferCardsJson { get; set; } = "{}";
    
    public string RequestCardsJson { get; set; } = "{}";

    public TradeStatus Status { get; set; } = TradeStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
