using Godot;
using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class SkySteerTests
{
    [Fact]
    public void ACursorAboveTheCharacterSteersAwayFromTheCamera()
    {
        var heading = SkySteer.Heading(0f, new Vector2(0f, -100f));
        Assert.Equal(0f, heading.X, 3);
        Assert.Equal(-1f, heading.Z, 3);
    }

    [Fact]
    public void ACursorToTheRightSteersToTheCamerasRight()
    {
        var heading = SkySteer.Heading(0f, new Vector2(100f, 0f));
        Assert.Equal(1f, heading.X, 3);
        Assert.Equal(0f, heading.Y, 3);
    }
}
