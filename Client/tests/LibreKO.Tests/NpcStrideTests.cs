using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class NpcStrideTests
{
    private const double StepAt = 100.0;
    private const double ServerStep = 0.25;
    private const double LateStep = 0.06;

    [Fact]
    public void AnNpcStillTravellingToItsStepStrides()
    {
        Assert.True(NpcStride.Continues(true, StepAt + 5.0, StepAt));
    }

    [Fact]
    public void AnNpcThatReachedItsStepBeforeTheNextOneArrivesKeepsStriding()
    {
        Assert.True(NpcStride.Continues(false, StepAt + ServerStep, StepAt));
        Assert.True(NpcStride.Continues(false, StepAt + ServerStep + LateStep, StepAt));
    }

    [Fact]
    public void AnNpcWithNoFurtherStepStopsStriding()
    {
        Assert.False(NpcStride.Continues(false, StepAt + NpcStride.Hold, StepAt));
        Assert.False(NpcStride.Continues(false, StepAt, NpcStride.NoStep));
    }
}
