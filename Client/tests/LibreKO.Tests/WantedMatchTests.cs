using LibreKO.Domain;
using LibreKO.Network;
using Xunit;

namespace LibreKO.Tests;

public class WantedMatchTests
{
    private const int Key = 910_000_000;
    private const int Potion = 389_010_000;

    [Fact]
    public void TheFirstBagSlotHoldingTheWantedItemIsPicked()
    {
        var bag = new[] { default, new ItemSlot { ItemId = Potion, Count = 3 }, new ItemSlot { ItemId = Key, Count = 1 }, new ItemSlot { ItemId = Key, Count = 9 } };

        Assert.Equal(2, WantedMatch.FirstBagSlot(bag, Key));
        Assert.Equal(-1, WantedMatch.FirstBagSlot(bag, 1));
        Assert.Equal(-1, WantedMatch.FirstBagSlot(bag, 0));
    }

    [Fact]
    public void TheFirstWantedLineForTheItemIsPicked()
    {
        var wanted = new[] { default, new MerchantStallItem { ItemId = Key, Count = 5, Price = 100 } };

        Assert.Equal(1, WantedMatch.FirstWanted(wanted, Key));
        Assert.Equal(-1, WantedMatch.FirstWanted(wanted, Potion));
    }
}
