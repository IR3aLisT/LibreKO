using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class ObjectTurnTests
{
    private const float FullTurn = System.MathF.PI * 2f;

    [Fact]
    public void IncreasingTurnsGoUpEvenWhenTheTargetIsBehind()
    {
        float target = ObjectTurn.TargetRadians(90);
        Assert.Equal(3f - FullTurn, ObjectTurn.StartFrom(3f, target, ObjectTurn.Increasing), 4);
        Assert.Equal(1f, ObjectTurn.StartFrom(1f, target, ObjectTurn.Increasing), 4);
    }

    [Fact]
    public void DecreasingTurnsGoDownEvenWhenTheTargetIsAhead()
    {
        float target = ObjectTurn.TargetRadians(90);
        Assert.Equal(1f + FullTurn, ObjectTurn.StartFrom(1f, target, ObjectTurn.Decreasing), 4);
        Assert.Equal(3f, ObjectTurn.StartFrom(3f, target, ObjectTurn.Decreasing), 4);
    }

    [Fact]
    public void ObjectsTurnAtOneRadianASecond()
    {
        Assert.Equal(System.Math.PI / 2, ObjectTurn.Seconds(0f, ObjectTurn.TargetRadians(90)), 3);
    }
}
