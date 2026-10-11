using Godot;
using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class UnitBlockingTests
{
    private static readonly Vector2 Unit = Vector2.Zero;

    [Fact]
    public void AStepIntoAUnitsSpaceIsBlocked()
    {
        Assert.True(UnitBlocking.Blocks(new Vector2(2f, 0f), new Vector2(1.3f, 0f), Unit, 1.8f, 1.8f));
    }

    [Fact]
    public void SteppingAwayOrPastTheSpaceIsAllowed()
    {
        Assert.False(UnitBlocking.Blocks(new Vector2(1.3f, 0f), new Vector2(1.4f, 0f), Unit, 1.8f, 1.8f));
        Assert.False(UnitBlocking.Blocks(new Vector2(3f, 0f), new Vector2(2f, 0f), Unit, 1.8f, 1.8f));
    }

    [Fact]
    public void BigMonstersKeepYouFurtherAway()
    {
        Assert.True(UnitBlocking.Blocks(new Vector2(4f, 0f), new Vector2(3.5f, 0f), Unit, 1.8f, 8f));
        Assert.False(UnitBlocking.Blocks(new Vector2(4f, 0f), new Vector2(3.5f, 0f), Unit, 1.8f, 1.8f));
    }
}
