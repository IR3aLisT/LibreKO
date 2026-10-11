using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class GenieTownReturnTests
{
    private static GenieTownReturn.State Calm() => new(
        HpStock: 50, HpThreshold: 10, MpStock: 50, MpThreshold: 10,
        NeedsArrows: false, Arrows: 0,
        WasInParty: false, InParty: false, PartyOthersAllDead: false,
        WeaponBroken: false);

    private const GenieOther Everything = GenieOther.HpPotions | GenieOther.MpPotions | GenieOther.Arrows
        | GenieOther.PartyBroken | GenieOther.LastSurvivor | GenieOther.WeaponBroken;

    [Theory]
    [InlineData(true, 9, 10, true)]
    [InlineData(true, 10, 10, false)]
    [InlineData(false, 0, 10, false)]
    public void TheGenieGoesHomeOnlyWhenTheStockFallsBelowTheSetValue(bool enabled, int stock, int threshold, bool expected)
    {
        Assert.Equal(expected, GenieTownReturn.ShouldReturn(enabled, stock, threshold));
    }

    [Theory]
    [InlineData(0, GenieTownReturn.DefaultCount)]
    [InlineData(25, 25)]
    [InlineData(5000, GenieTownReturn.DefaultCount)]
    public void AnEmptyOrBrokenStoredCountFallsBackToTen(int stored, int expected)
    {
        Assert.Equal(expected, GenieTownReturn.StoredOrDefault(stored));
    }

    [Fact]
    public void NothingFiresWhileAllIsWell()
    {
        Assert.Equal(GenieOther.None, GenieTownReturn.Triggered(Everything, Calm()));
    }

    [Fact]
    public void EachRuleFiresOnlyWhenItIsOn()
    {
        var noArrows = Calm() with { NeedsArrows = true, Arrows = 0 };
        Assert.Equal(GenieOther.Arrows, GenieTownReturn.Triggered(GenieOther.Arrows, noArrows));
        Assert.Equal(GenieOther.None, GenieTownReturn.Triggered(GenieOther.MpPotions, noArrows));

        var partyGone = Calm() with { WasInParty = true, InParty = false };
        Assert.Equal(GenieOther.PartyBroken, GenieTownReturn.Triggered(Everything, partyGone));

        var alone = Calm() with { WasInParty = true, InParty = true, PartyOthersAllDead = true };
        Assert.Equal(GenieOther.LastSurvivor, GenieTownReturn.Triggered(Everything, alone));

        var broken = Calm() with { WeaponBroken = true };
        Assert.Equal(GenieOther.WeaponBroken, GenieTownReturn.Triggered(Everything, broken));
    }

    [Fact]
    public void AnArcherWithoutABowNeverRunsOutOfArrows()
    {
        var daggers = Calm() with { NeedsArrows = false, Arrows = 0 };
        Assert.Equal(GenieOther.None, GenieTownReturn.Triggered(GenieOther.Arrows, daggers));
    }

    [Theory]
    [InlineData(500, 1000, true)]
    [InlineData(499, 1000, false)]
    [InlineData(0, 0, false)]
    public void TheMoveToTownNeedsHalfTheHp(int hp, int maxHp, bool expected)
    {
        Assert.Equal(expected, GenieTownReturn.CanMoveToTown(hp, maxHp));
    }

    [Theory]
    [InlineData(GenieOther.AutoSpirit, 1, true, true)]
    [InlineData(GenieOther.AutoSpirit, 2, true, false)]
    [InlineData(GenieOther.AutoSpirit, 1, false, false)]
    [InlineData(GenieOther.None, 1, true, false)]
    public void TheSpiritIsUsedAtTheLastPointWhenOneIsCarried(GenieOther enabled, int points, bool hasSpirit, bool expected)
    {
        Assert.Equal(expected, GenieTownReturn.WantsSpirit(enabled, points, hasSpirit));
    }
}
