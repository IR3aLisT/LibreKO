using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class TurningTests
{
    private const float Frame = 1f / 60f;

    [Fact]
    public void AFacingTurnMovesAtMostTheRateEachFrame()
    {
        Assert.Equal(4.5f, Turning.Toward(0f, 90f, Frame), 3);
        Assert.Equal(-4.5f, Turning.Toward(0f, -90f, Frame), 3);
    }

    [Fact]
    public void TheShortWayRoundIsTaken()
    {
        Assert.True(Turning.Toward(170f, -170f, Frame) > 170f);
        Assert.True(Turning.Toward(-170f, 170f, Frame) < -170f);
    }

    [Fact]
    public void ALastStepWithinTheRateLandsOnTheGoal()
    {
        Assert.Equal(30f, Turning.Toward(28f, 30f, Frame));
        Assert.Equal(-180f, Turning.Toward(178f, -180f, Frame));
    }

    [Fact]
    public void AHalfTurnTakesTwoThirdsOfASecond()
    {
        float yaw = 0f;
        int frames = 0;
        while (yaw != 180f && frames++ < 600) yaw = Turning.Toward(yaw, 180f, Frame);
        Assert.InRange(frames, 39, 41);
    }

    [Theory]
    [InlineData(190f, -170f)]
    [InlineData(-190f, 170f)]
    [InlineData(720f, 0f)]
    [InlineData(45f, 45f)]
    public void AnglesWrapIntoHalfTurns(float degrees, float wrapped)
    {
        Assert.Equal(wrapped, Turning.Wrap(degrees), 3);
    }
}
