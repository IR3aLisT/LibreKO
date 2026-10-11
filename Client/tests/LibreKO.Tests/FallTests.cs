using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class FallTests
{
    private const float Frame = 1f / 60f;

    [Fact]
    public void ALedgeIsFallenOverSeveralFramesFasterAndFaster()
    {
        float speed = 0f, y = 10f;
        float first = 10f - Fall.Step(ref speed, y, 0f, Frame);
        y = 10f - first;
        float second = y - Fall.Step(ref speed, y, 0f, Frame);
        Assert.True(second > first);
        Assert.True(first > 0f);
    }

    [Fact]
    public void TheFallEndsOnTheGroundAndStops()
    {
        float speed = 0f, y = 10f;
        int frames = 0;
        while (y > 0f && frames++ < 600) y = Fall.Step(ref speed, y, 0f, Frame);
        Assert.Equal(0f, y);
        Assert.Equal(0f, speed);
        Assert.InRange(frames, 5, 30);
    }

    [Fact]
    public void GroundAboveSnapsUp()
    {
        float speed = 3f;
        Assert.Equal(5f, Fall.Step(ref speed, 4f, 5f, Frame));
        Assert.Equal(0f, speed);
    }
}
