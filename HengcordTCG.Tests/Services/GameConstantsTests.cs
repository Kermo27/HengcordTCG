using FluentAssertions;
using HengcordTCG.Shared;

namespace HengcordTCG.Tests.Services;

public class GameConstantsTests
{
    [Fact]
    public void DailyCooldownHours_Is20()
    {
        GameConstants.DailyCooldownHours.Should().Be(20);
    }

    [Fact]
    public void DailyCooldownHours_IsPositive()
    {
        GameConstants.DailyCooldownHours.Should().BeGreaterThan(0);
    }
}
