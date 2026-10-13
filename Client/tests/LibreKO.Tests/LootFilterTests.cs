using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class LootFilterTests
{
    private const int Sword = 110110001;
    private const int Potion = 389010000;

    [Fact]
    public void AnOffFilterTakesEverything()
    {
        var filter = new LootFilter { MinSellPrice = 1_000_000 };
        Assert.True(filter.Takes(Potion, LootGroup.Other, ItemData.Rarity.Regular, 10));
    }

    [Fact]
    public void GroupsGradesAndPriceDecideUnlessARuleSaysOtherwise()
    {
        var filter = new LootFilter { Enabled = true, MinSellPrice = 500 };
        filter.Grades.Remove(ItemData.Rarity.Regular);
        filter.Groups.Remove(LootGroup.Other);

        Assert.False(filter.Takes(Sword, LootGroup.Equipment, ItemData.Rarity.Regular, 5_000));
        Assert.True(filter.Takes(Sword, LootGroup.Equipment, ItemData.Rarity.Rare, 5_000));
        Assert.False(filter.Takes(Sword, LootGroup.Equipment, ItemData.Rarity.Rare, 100));
        Assert.False(filter.Takes(Potion, LootGroup.Other, ItemData.Rarity.Regular, 5_000));
        Assert.True(filter.Takes(0, LootGroup.Coins, ItemData.Rarity.Regular, 0));

        filter.SetRule(Potion, true);
        Assert.True(filter.Takes(Potion, LootGroup.Other, ItemData.Rarity.Regular, 0));
        filter.SetRule(Sword, false);
        Assert.False(filter.Takes(Sword, LootGroup.Equipment, ItemData.Rarity.Rare, 5_000));
        filter.SetRule(Sword, null);
        Assert.True(filter.Takes(Sword, LootGroup.Equipment, ItemData.Rarity.Rare, 5_000));
    }

    [Fact]
    public void AFlipSetsTheSmallestRuleThatChangesTheOutcome()
    {
        var filter = new LootFilter { Enabled = true, MinSellPrice = 500 };

        Assert.True(filter.Flip(Potion, LootGroup.Other, ItemData.Rarity.Regular, 5_000));
        Assert.Contains(Potion, filter.Never);
        Assert.True(filter.Flip(Potion, LootGroup.Other, ItemData.Rarity.Regular, 5_000));
        Assert.DoesNotContain(Potion, filter.Never);
        Assert.Empty(filter.Always);

        Assert.True(filter.Flip(Potion, LootGroup.Other, ItemData.Rarity.Regular, 10));
        Assert.Contains(Potion, filter.Always);
        Assert.Equal(LootReason.AlwaysRule, filter.Verdict(Potion, LootGroup.Other, ItemData.Rarity.Regular, 10));
        Assert.True(filter.Flip(Potion, LootGroup.Other, ItemData.Rarity.Regular, 10));
        Assert.Equal(LootReason.TooCheap, filter.Verdict(Potion, LootGroup.Other, ItemData.Rarity.Regular, 10));
    }

    [Fact]
    public void RulesStopAtTheLimit()
    {
        var filter = new LootFilter { Enabled = true };
        for (int i = 0; i < LootFilter.MaxRules; i++) filter.SetRule(i + 1, true);

        Assert.False(filter.Flip(Potion, LootGroup.Other, ItemData.Rarity.Regular, 5_000));
    }

    [Fact]
    public void QuestItemsIgnoreThePrice()
    {
        var filter = new LootFilter { Enabled = true, MinSellPrice = 500 };
        Assert.True(filter.Takes(Potion, LootGroup.Quest, ItemData.Rarity.Regular, 0));
    }
}
