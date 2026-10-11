using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class RepairPriceTests
{
    [Fact]
    public void AWornWeaponIsListed() =>
        Assert.True(RepairPrice.Listed(chargeItem: false, noRepairSale: false, maxDurability: 7000, durability: 3000, cost: 412));

    [Fact]
    public void ChargeItemsAreLeftOut() =>
        Assert.False(RepairPrice.Listed(chargeItem: true, noRepairSale: false, maxDurability: 50, durability: 10, cost: 30));

    [Fact]
    public void AFreeRepairIsLeftOut() =>
        Assert.False(RepairPrice.Listed(chargeItem: false, noRepairSale: false, maxDurability: 7000, durability: 6999, cost: 0));

    [Fact]
    public void NoRepairSaleItemsAreLeftOut() =>
        Assert.False(RepairPrice.Listed(chargeItem: false, noRepairSale: true, maxDurability: 7000, durability: 3000, cost: 412));

    [Fact]
    public void AnUndamagedItemCostsNothing()
    {
        Assert.Equal(0, RepairPrice.Cost(20000, 8000, 8000));
        Assert.Equal(0, RepairPrice.Cost(20000, 0, 0));
    }

    [Fact]
    public void TheCostGrowsWithTheMissingDurability()
    {
        int half = RepairPrice.Cost(20000, 8000, 4000);
        int full = RepairPrice.Cost(20000, 8000, 0);
        Assert.True(half > 0);
        Assert.InRange(full, half * 2 - 1, half * 2 + 1);
    }
}
