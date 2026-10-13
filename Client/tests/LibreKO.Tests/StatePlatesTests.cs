using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class StatePlatesTests
{
    [Theory]
    [InlineData(21, true)]
    [InlineData(31, false)]
    [InlineData(35, false)]
    [InlineData(45, false)]
    [InlineData(46, false)]
    [InlineData(85, false)]
    public void TitlesHideInTheZones2619Names(int zone, bool shown) =>
        Assert.Equal(shown, StatePlates.TitleShownIn(zone));

    [Fact]
    public void ZoneTwentyNineSidesPickTheirNames()
    {
        Assert.Equal(42154, StatePlates.ExpeditionNameText(StatePlates.Expedition));
        Assert.Equal(42155, StatePlates.ExpeditionNameText(StatePlates.ContaminatedResident));
        Assert.Equal(0, StatePlates.ExpeditionNameText(2));
    }
}
