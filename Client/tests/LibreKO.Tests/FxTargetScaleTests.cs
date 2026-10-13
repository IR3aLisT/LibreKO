using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class FxTargetScaleTests
{
    [Theory]
    [InlineData(1f, 1f, 1.8f, 1.8f)]
    [InlineData(4f, 3f, 2f, 4f)]
    [InlineData(30f, 10f, 20f, FxTargetScale.MaxScale)]
    public void TheSizeIsTheWidestOrTallestSideCappedAtSixteen(float width, float depth, float height, float size)
    {
        Assert.Equal(size, FxTargetScale.WorldSize(width, depth, height), 4);
    }

    [Fact]
    public void APinnedEffectDividesOutTheBodyScaleItAlreadyInherits()
    {
        Assert.Equal(2f, FxTargetScale.ForPinned(6f, 3f), 4);
        Assert.Equal(6f, FxTargetScale.ForPinned(6f, 0f), 4);
    }
}
