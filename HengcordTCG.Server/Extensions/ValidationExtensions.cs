using System.ComponentModel.DataAnnotations;

namespace HengcordTCG.Server.Extensions;

public static class ValidationExtensions
{
    public static void ValidateDiscordId(ulong discordId)
    {
        if (discordId == 0)
            throw new ArgumentException("Discord ID must be valid (non-zero)", nameof(discordId));
    }

    public static void ValidateUsername(string? username)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username is required and cannot be empty", nameof(username));

        if (username.Length > 100)
            throw new ArgumentException("Username must not exceed 100 characters", nameof(username));
    }

    public static void ValidatePackName(string? packName)
    {
        if (string.IsNullOrWhiteSpace(packName))
            throw new ArgumentException("Pack name is required and cannot be empty", nameof(packName));

        if (packName.Length > 100)
            throw new ArgumentException("Pack name must not exceed 100 characters", nameof(packName));
    }

    public static void ValidateCardName(string? cardName)
    {
        if (string.IsNullOrWhiteSpace(cardName))
            throw new ArgumentException("Card name is required and cannot be empty", nameof(cardName));

        if (cardName.Length > 200)
            throw new ArgumentException("Card name must not exceed 200 characters", nameof(cardName));
    }

    public static void ValidateAmount(long amount, string paramName = "amount")
    {
        if (amount <= 0)
            throw new ArgumentException($"{paramName} must be greater than 0", paramName);

        if (amount > long.MaxValue)
            throw new ArgumentException($"{paramName} exceeds maximum value", paramName);
    }

    public static void ValidateLimitParameter(int limit, string paramName = "limit")
    {
        if (limit < 1)
            throw new ArgumentException($"{paramName} must be at least 1", paramName);

        if (limit > 1000)
            throw new ArgumentException($"{paramName} cannot exceed 1000", paramName);
    }
}
