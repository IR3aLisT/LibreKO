using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class SwingTimingTests
{
    [Theory]
    [InlineData(0, 0, 1f, 1f)]
    [InlineData(100, 100, 1f, 1f)]
    [InlineData(150, 100, 1f, 1.5f)]
    [InlineData(200, 90, 1f, 1.8f)]
    [InlineData(150, 0, 1f, 1.5f)]
    [InlineData(150, 100, 1.5f, 1f)]
    [InlineData(0, 0, 2f, 0.5f)]
    public void TheWeaponDelayAndItsExtPercentSetTheSwingInterval(int delay, int percent, float speed, float expected)
    {
        Assert.Equal(expected, SwingTiming.IntervalSeconds(delay, percent, speed), 3);
    }
}
