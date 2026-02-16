namespace HengcordTCG.Shared.DTOs.Trades;

public record CreateTradeRequest(
    ulong InitiatorId,
    string InitiatorName,
    ulong TargetId,
    string TargetName,
    string Offer,
    string Request
);
