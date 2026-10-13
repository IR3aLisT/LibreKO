using Godot;
using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class StrikePointTests
{
    private static readonly Aabb Body = new(new Vector3(-0.5f, 0f, -0.5f), new Vector3(1f, 2f, 1f));

    [Fact]
    public void ABladeEntersTheBoxWhereItFirstCrossesIt()
    {
        var entry = StrikePoint.Entry(Body, new Vector3(-2f, 1f, 0f), new Vector3(2f, 1f, 0f));

        Assert.NotNull(entry);
        Assert.Equal(-0.5f, entry!.Value.X, 4);
        Assert.Equal(1f, entry.Value.Y, 4);
    }

    [Fact]
    public void ABladeThatMissesHasNoEntry()
    {
        Assert.Null(StrikePoint.Entry(Body, new Vector3(-2f, 3f, 0f), new Vector3(2f, 3f, 0f)));
        Assert.Null(StrikePoint.Entry(Body, new Vector3(-3f, 1f, 0f), new Vector3(-1f, 1f, 0f)));
    }

    [Fact]
    public void ABladeStartingInsideEntersAtItsStart()
    {
        var start = new Vector3(0f, 1f, 0f);

        Assert.Equal(start, StrikePoint.Entry(Body, start, new Vector3(3f, 1f, 0f)));
    }

    [Fact]
    public void TheTraceIsExtendedByItsOwnLength()
    {
        Assert.Equal(new Vector3(0f, 3f, 0f), StrikePoint.Extended(new Vector3(0f, 1f, 0f), new Vector3(0f, 2f, 0f)));
    }

    [Fact]
    public void TheFallbackSitsAThirdOfTheHeightTowardTheAttacker()
    {
        var point = StrikePoint.TowardAttacker(Body, new Vector3(5f, 1f, 0f));

        Assert.Equal(2f / 3f, point.X, 4);
        Assert.Equal(1f, point.Y, 4);
    }
}
