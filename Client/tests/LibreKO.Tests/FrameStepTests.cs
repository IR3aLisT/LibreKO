using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class FrameStepTests
{
    [Theory]
    [InlineData(1.0 / 144.0)]
    [InlineData(1.0 / 60.0)]
    [InlineData(0.25)]
    [InlineData(0.999)]
    public void OrdinaryFramesKeepTheirTime(double delta)
    {
        Assert.Equal(delta, FrameStep.Clamp(delta));
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(4.5)]
    public void AStallOfASecondOrMoreMovesOneThirtiethOfASecond(double delta)
    {
        Assert.Equal(1.0 / 30.0, FrameStep.Clamp(delta));
    }
}
