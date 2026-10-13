using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class RespawnBlinkTests
{
    [Theory]
    [InlineData(0.0, RespawnBlink.Alpha)]
    [InlineData(0.49, RespawnBlink.Alpha)]
    [InlineData(0.5, RespawnBlink.Alpha * RespawnBlink.DimFactor)]
    [InlineData(0.99, RespawnBlink.Alpha * RespawnBlink.DimFactor)]
    [InlineData(1.0, RespawnBlink.Alpha)]
    public void TheBodyAlternatesEveryHalfSecond(double elapsed, float expected) =>
        Assert.Equal(expected, RespawnBlink.AlphaAt(elapsed), 4);

    [Theory]
    [InlineData(RespawnBlink.Normal, true)]
    [InlineData(RespawnBlink.ChaosNormal, true)]
    [InlineData(2, false)]
    [InlineData(RespawnBlink.Blinking, false)]
    public void NormalStatesEndTheBlink(int abnormal, bool ends) =>
        Assert.Equal(ends, RespawnBlink.Ends(abnormal));
}
