using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class SellUnitTests
{
    [Theory]
    [InlineData(6000, false, false, 1000)]
    [InlineData(6000, false, true, 1500)]
    [InlineData(6000, true, true, 6000)]
    [InlineData(5, false, false, 0)]
    public void APremiumSellerIsQuotedAQuarterOfTheBuyPrice(int buy, bool fullSale, bool premium, int unit) =>
        Assert.Equal(unit, ItemData.SellUnit(buy, fullSale, premium));
}
