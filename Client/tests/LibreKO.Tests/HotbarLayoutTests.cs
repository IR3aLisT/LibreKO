using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class HotbarLayoutTests
{
    private const int Nova = 110560;
    private const int Heal = 111503;

    [Fact]
    public void AnOldEightSlotBarKeepsEachSkillOnItsPageAndSlot()
    {
        var old = new int[HotbarLayout.LegacyTotal];
        old[0] = Nova;
        old[3 * HotbarLayout.LegacySlotsPerPage + 7] = Heal;

        var slots = HotbarLayout.Normalize(old);

        Assert.Equal(HotbarLayout.Total, slots.Length);
        Assert.Equal(Nova, slots[0]);
        Assert.Equal(Heal, slots[3 * HotbarLayout.SlotsPerPage + 7]);
        Assert.Equal(0, slots[3 * HotbarLayout.SlotsPerPage + 8]);
    }

    [Fact]
    public void ATenSlotBarIsKeptAsItIs()
    {
        var saved = new int[HotbarLayout.Total];
        saved[HotbarLayout.Total - 1] = Nova;
        Assert.Equal(saved, HotbarLayout.Normalize(saved));
    }

    [Fact]
    public void TheTenthSlotIsKeyZero()
    {
        Assert.Equal("1", HotbarLayout.KeyLabel(0));
        Assert.Equal("9", HotbarLayout.KeyLabel(8));
        Assert.Equal("0", HotbarLayout.KeyLabel(9));
    }

    [Theory]
    [InlineData(0.5f, 0.7f)]
    [InlineData(1.02f, 1f)]
    [InlineData(1.33f, 1.35f)]
    [InlineData(2f, 1.5f)]
    public void TheBarSizeStaysBetween70And150PercentInFives(float asked, float kept) =>
        Assert.Equal(kept, HotbarLayout.ClampScale(asked), 3);

    [Fact]
    public void TheGapIsClampedAndHalvedWhenStanding()
    {
        Assert.Equal(HotbarLayout.GapMax, HotbarLayout.ClampGap(40));
        Assert.Equal(HotbarLayout.GapMin, HotbarLayout.ClampGap(-3));
        Assert.Equal(HotbarLayout.GapDefault, HotbarLayout.SlotGap(HotbarLayout.GapDefault, vertical: false));
        Assert.Equal(3, HotbarLayout.SlotGap(HotbarLayout.GapDefault, vertical: true));
    }
}
