using FluentAssertions;
using LibreKO.Game.World;

namespace LibreKO.Game.Tests;

public class NpcSellRulesTests
{
    [Theory]
    [InlineData(6000, false, false, 1000)]
    [InlineData(6000, false, true, 1500)]
    [InlineData(6000, true, true, 6000)]
    [InlineData(5, false, false, 0)]
    public void APremiumSellerGetsAQuarterOfTheBuyPrice(int buy, bool fullSale, bool premium, int unit) =>
        NpcSellRules.Unit(buy, fullSale, premium).Should().Be(unit);

    [Fact]
    public void ThePremiumBonusIsWhatTheQuarterAdds() => NpcSellRules.PremiumBonusPercent.Should().Be(50);
}
