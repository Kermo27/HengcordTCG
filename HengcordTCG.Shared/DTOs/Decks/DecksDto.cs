namespace HengcordTCG.Shared.DTOs.Decks;

public record SaveDeckRequest(
    ulong DiscordId,
    string? Name,
    int CommanderId,
    List<int> MainDeckCardIds,
    List<int> CloserCardIds
);
