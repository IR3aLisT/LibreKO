using Godot;
using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class PickBoxTests
{
    [Fact]
    public void TheBoxSpansTheJointsAndWidensThinAxes()
    {
        var box = PickBox.FromJoints(new[] { new Vector3(0f, 0f, 0f), new Vector3(0.2f, 1.8f, 0.1f), new Vector3(-0.6f, 1.2f, 0f) });

        Assert.Equal(-0.6f, box.Position.X, 3);
        Assert.Equal(0.2f, box.End.X, 3);
        Assert.Equal(0f, box.Position.Y, 3);
        Assert.Equal(1.8f, box.End.Y, 3);
        Assert.Equal(-PickBox.Widen, box.Position.Z, 3);
        Assert.Equal(0.1f + PickBox.Widen, box.End.Z, 3);
    }

    [Fact]
    public void ARayThroughTheBoxHitsItAtItsNearSide()
    {
        var box = new Aabb(new Vector3(-1f, 0f, -1f), new Vector3(2f, 2f, 2f));
        float? hit = PickBox.Hit(new Vector3(0f, 1f, -10f), new Vector3(0f, 0f, 1f), box);
        Assert.NotNull(hit);
        Assert.Equal(9f, hit!.Value, 3);
    }

    [Fact]
    public void ARayBesideTheBoxMissesIt() =>
        Assert.Null(PickBox.Hit(new Vector3(3f, 1f, -10f), new Vector3(0f, 0f, 1f), new Aabb(new Vector3(-1f, 0f, -1f), new Vector3(2f, 2f, 2f))));

    [Fact]
    public void ARayPointingAwayMissesIt() =>
        Assert.Null(PickBox.Hit(new Vector3(0f, 1f, -10f), new Vector3(0f, 0f, -1f), new Aabb(new Vector3(-1f, 0f, -1f), new Vector3(2f, 2f, 2f))));

    [Fact]
    public void PetsGasAndFixedPosesAreNeverPicked()
    {
        Assert.True(PickBox.SkippedType(PickBox.PetType));
        Assert.True(PickBox.SkippedType(PickBox.PoisonGasType));
        Assert.False(PickBox.SkippedType(0));
    }

    [Fact]
    public void RestrictedNpcsArePickedOnlyAsAllowed()
    {
        var restriction = new NpcRestriction();
        restriction.Unpickable.Add(32153);
        restriction.PickTransformed.Add(30326);

        Assert.False(restriction.Pickable(32153, transformed: true));
        Assert.False(restriction.Pickable(30326, transformed: false));
        Assert.True(restriction.Pickable(30326, transformed: true));
        Assert.True(restriction.Pickable(1, transformed: false));
    }
}
