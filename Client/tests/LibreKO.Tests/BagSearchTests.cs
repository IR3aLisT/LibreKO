using System.Collections.Generic;
using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class BagSearchTests
{
    [Fact]
    public void AHitInsideAMagicBagNamesThatBag()
    {
        int secondBag = InventoryConstants.MagicBagStart + InventoryConstants.MagicBagMax + 2;

        var bags = BagSearch.BagsHoldingHits(new HashSet<int> { secondBag });

        Assert.Equal(new[] { false, true, false }, bags);
    }

    [Fact]
    public void HitsInTheMainBagNameNoMagicBag()
    {
        var bags = BagSearch.BagsHoldingHits(new HashSet<int> { InventoryConstants.InventoryStart + 3 });

        Assert.DoesNotContain(true, bags);
    }

    [Fact]
    public void OnlyBagAndMagicBagSlotsAreSearched()
    {
        Assert.True(BagSearch.Searchable(InventoryConstants.InventoryStart));
        Assert.True(BagSearch.Searchable(InventoryConstants.MagicBagStart));
        Assert.False(BagSearch.Searchable(0));
        Assert.False(BagSearch.Searchable(InventoryConstants.CospreStart));
    }
}
