using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class SizeStateTests
{
    private const int Human = 12;
    private const int KarusKurian = 6;
    private const int ElMoradPortu = 14;
    private const int Moradon = 21;
    private const int ChaosDungeon = 85;

    [Theory]
    [InlineData(SizeState.Giant, 2f, 2f, 2f)]
    [InlineData(SizeState.GiantTarget, 2f, 2f, 2f)]
    [InlineData(SizeState.Dwarf, 0.5f, 0.5f, 0.5f)]
    [InlineData(SizeState.FlatSideways, 0.1f, 1f, 1.3f)]
    [InlineData(SizeState.FlatForward, 1.3f, 1f, 0.1f)]
    [InlineData(SizeState.Squashed, 1f, 0.7f, 1.4f)]
    public void EachSizeStateHasItsScale(int state, float x, float y, float z)
    {
        Assert.Equal(new SizeState.Scale(x, y, z), SizeState.Target(state, Human, Moradon));
    }

    [Fact]
    public void KuriansAndPortusStandTenPercentLarger()
    {
        Assert.Equal(SizeState.Scale.Uniform(1.1f), SizeState.Target(SizeState.Normal, KarusKurian, Moradon));
        Assert.Equal(SizeState.Scale.Uniform(1.1f), SizeState.Target(SizeState.Normal, ElMoradPortu, Moradon));
        Assert.Equal(SizeState.Scale.Uniform(1f), SizeState.Target(SizeState.Normal, Human, Moradon));
    }

    [Fact]
    public void ChaosNormalKeepsTheSizeInsideTheChaosDungeon()
    {
        Assert.Null(SizeState.Target(SizeState.ChaosNormal, Human, ChaosDungeon));
        Assert.Equal(SizeState.Scale.Uniform(1f), SizeState.Target(SizeState.ChaosNormal, Human, Moradon));
    }

    [Fact]
    public void ABlinkingRecordSpawnsAtNormalSize()
    {
        Assert.Equal(SizeState.Normal, SizeState.OnSpawn(RespawnBlink.Blinking, Human, Moradon));
        Assert.Equal(SizeState.Giant, SizeState.OnSpawn(SizeState.Giant, Human, Moradon));
    }

    [Theory]
    [InlineData(-0.1, 0f)]
    [InlineData(0.25, 0.25f)]
    [InlineData(1.0, 1f)]
    [InlineData(3.0, 1f)]
    public void TheScaleGlidesLinearlyOverOneSecond(double elapsed, float expected)
    {
        Assert.Equal(expected, SizeState.Progress(elapsed), 3);
    }
}
