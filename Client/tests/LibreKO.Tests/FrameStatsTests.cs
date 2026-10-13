using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class FrameStatsTests
{
    [Fact]
    public void PercentilesPickTheNearestRankedFrame()
    {
        var frames = new double[] { 0.010, 0.012, 0.011, 0.050, 0.013, 0.014, 0.010, 0.016, 0.012, 0.100 };
        Assert.Equal(0.012, FrameStats.Percentile(frames, 0.5), 6);
        Assert.Equal(0.100, FrameStats.Percentile(frames, 0.95), 6);
        Assert.Equal(0.100, FrameStats.Percentile(frames, 0.99), 6);
        Assert.Equal(0.0248, FrameStats.Mean(frames), 6);
    }

    [Fact]
    public void NoFramesGiveZero()
    {
        Assert.Equal(0, FrameStats.Percentile(System.Array.Empty<double>(), 0.95));
        Assert.Equal(0, FrameStats.Mean(System.Array.Empty<double>()));
    }
}
