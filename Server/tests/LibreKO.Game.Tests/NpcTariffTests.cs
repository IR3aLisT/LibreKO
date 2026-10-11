using FluentAssertions;
using LibreKO.Game.World;
using Xunit;

namespace LibreKO.Game.Tests;

public class NpcTariffTests
{
    [Theory]
    [InlineData(10_000, 0, 0)]
    [InlineData(10_000, 5, 500)]
    [InlineData(999, 10, 99)]
    public void TheTariffIsAPercentOfTheBill(long total, int tariff, int tax) =>
        NpcTariff.Tax(total, tariff).Should().Be(tax);

    [Fact]
    public void TheKingTakesEightyPercentIntoTheTreasuryAndTwentyAsTerritoryTax() =>
        NpcTariff.KingSplit(500).Should().Be((400, 100));
}
