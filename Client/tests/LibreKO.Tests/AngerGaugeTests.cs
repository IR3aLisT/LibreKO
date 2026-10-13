using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class AngerGaugeTests
{
    [Theory]
    [InlineData(71, 60, true)]
    [InlineData(66, 50, true)]
    [InlineData(71, 49, false)]
    [InlineData(21, 80, false)]
    public void TheGaugeShowsInWarZonesFromLevelFifty(int zone, int level, bool shown) =>
        Assert.Equal(shown, AngerGauge.ShownIn(zone, level));

    [Theory]
    [InlineData(0, 4, 0)]
    [InlineData(3, 0, 6)]
    [InlineData(5, 0, AngerGauge.MaxImage)]
    [InlineData(6, 8, 8)]
    public void EachPointLightsTwoImages(int gauge, int current, int expected) =>
        Assert.Equal(expected, AngerGauge.ImageFor(gauge, current));

    [Fact]
    public void TheFullAnimationLoopsFourFrames() =>
        Assert.Equal(AngerGauge.FullFrame(0), AngerGauge.FullFrame(AngerGauge.FullFrames));
}
