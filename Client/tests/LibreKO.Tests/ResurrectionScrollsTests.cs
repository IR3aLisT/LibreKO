using System.Linq;
using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class ResurrectionScrollsTests
{
    private const int OpenZone = 21;
    private const int Level = 60;

    private static ScrollSkill Scroll(int id, int recover, int type = ResurrectionScrolls.SelfResurrect, int moral = SkillTarget.Self) =>
        new(id, $"Scroll {id}", moral, type, recover);

    [Fact]
    public void OnlySelfResurrectScrollsInTheRetailRangeAreOffered()
    {
        var carried = new[]
        {
            Scroll(480002, 50),
            Scroll(480013, 100, ResurrectionScrolls.LifeCrystal),
            Scroll(470002, 50),
            Scroll(480003, 60, moral: SkillTarget.Party),
            Scroll(ResurrectionScrolls.ExcludedSkill, 100),
            Scroll(480009, 90),
        };

        var offer = ResurrectionScrolls.Offer(carried, OpenZone, Level);

        Assert.Equal(new[] { 480002, 480009 }, offer.Select(s => s.SkillId));
    }

    [Fact]
    public void LifeCrystalsAreTheOnlyOfferInTheirZone()
    {
        var carried = new[] { Scroll(480002, 50), Scroll(480013, 100, ResurrectionScrolls.LifeCrystal) };

        var offer = ResurrectionScrolls.Offer(carried, ResurrectionScrolls.LifeCrystalZone, Level);

        Assert.Equal(new[] { 480013 }, offer.Select(s => s.SkillId));
    }

    [Fact]
    public void AtMostThreeDistinctScrollsAreOffered()
    {
        var carried = new[] { Scroll(480002, 50), Scroll(480002, 50), Scroll(480003, 60), Scroll(480005, 50), Scroll(480009, 90) };

        var offer = ResurrectionScrolls.Offer(carried, OpenZone, Level);

        Assert.Equal(new[] { 480002, 480003, 480005 }, offer.Select(s => s.SkillId));
    }

    [Fact]
    public void NoScrollsAreOfferedAtLevelFiveOrBelow() =>
        Assert.Empty(ResurrectionScrolls.Offer(new[] { Scroll(480002, 50) }, OpenZone, ResurrectionScrolls.MinLevelExclusive));

    [Fact]
    public void TheBestRecoveryIsPickedFirstWinningTies()
    {
        var offer = new[] { Scroll(480002, 50), Scroll(480009, 90), Scroll(480011, 90) };
        Assert.Equal(1, ResurrectionScrolls.BestIndex(offer));
        Assert.Equal(-1, ResurrectionScrolls.BestIndex(System.Array.Empty<ScrollSkill>()));
    }
}
