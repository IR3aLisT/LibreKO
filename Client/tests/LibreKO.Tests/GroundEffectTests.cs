using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class GroundEffectTests
{
    [Theory]
    [InlineData(GroundEffects.SubTerrain, 0, GroundEffect.None)]
    [InlineData(GroundEffects.SubTerrain, 4, GroundEffect.SwampSlow)]
    public void TheTerrainSubReadsItsEffect(byte sub, byte value, GroundEffect expected) =>
        Assert.Equal(expected, GroundEffects.Read(sub, value));

    [Theory]
    [InlineData(2, 1)]
    [InlineData(GroundEffects.SubTerrain, 5)]
    public void OtherSubsAndValuesAreIgnored(byte sub, byte value) =>
        Assert.Null(GroundEffects.Read(sub, value));

    [Fact]
    public void SwampsSlowAndNoneRestores()
    {
        Assert.Equal(GroundEffects.SlowFactor, GroundEffects.SpeedFactor(GroundEffect.Swamp, 1f));
        Assert.Equal(GroundEffects.SlowFactor, GroundEffects.SpeedFactor(GroundEffect.SwampSlow, 1f));
        Assert.Equal(1f, GroundEffects.SpeedFactor(GroundEffect.None, GroundEffects.SlowFactor));
    }

    [Fact]
    public void HayAndWaterLeaveTheSpeedAlone() =>
        Assert.Equal(GroundEffects.SlowFactor, GroundEffects.SpeedFactor(GroundEffect.Hay, GroundEffects.SlowFactor));

    [Fact]
    public void OnlyTheSlowSwampWearsTheAura()
    {
        Assert.True(GroundEffects.HasAura(GroundEffect.SwampSlow));
        Assert.False(GroundEffects.HasAura(GroundEffect.Swamp));
    }
}
