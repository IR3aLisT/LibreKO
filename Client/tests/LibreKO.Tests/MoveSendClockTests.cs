using Godot;
using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class MoveSendClockTests
{
    private static readonly Vector2 East = new(1f, 0f);
    private static readonly Vector2 North = new(0f, 1f);

    [Fact]
    public void TheFirstMoveIsSentAtOnce()
    {
        var clock = new MoveSendClock();

        Assert.True(clock.MoveDue(Vector2.Zero, 0f, East));
    }

    [Fact]
    public void AStraightRunIsResentOnlyWhenTheOneAndAHalfSecondsRunOut()
    {
        var clock = new MoveSendClock();
        clock.Sent(Vector2.Zero, new Vector2(6.75f, 0f), 0f);

        clock.Tick(1.4f);
        Assert.False(clock.MoveDue(new Vector2(6.3f, 0f), 0f, East));

        clock.Tick(0.2f);
        Assert.True(clock.MoveDue(new Vector2(7.2f, 0f), 0f, East));
    }

    [Fact]
    public void ATurnSharperThanTheDotLimitIsSentEarly()
    {
        var clock = new MoveSendClock();
        clock.Sent(Vector2.Zero, new Vector2(6.75f, 0f), 0f);

        clock.Tick(0.2f);
        Assert.False(clock.MoveDue(new Vector2(0.9f, 0f), 0f, North));

        clock.Tick(0.15f);
        Assert.True(clock.MoveDue(new Vector2(1.5f, 0f), 0f, North));

        clock.Sent(new Vector2(1.5f, 0f), new Vector2(1.5f, 6.75f), 0f);
        clock.Tick(0.15f);
        Assert.True(clock.MoveDue(new Vector2(1.5f, 0.7f), 0f, East));
    }

    [Fact]
    public void StandingStillSendsNothingWhenTheClockRunsOut()
    {
        var clock = new MoveSendClock();
        clock.Sent(new Vector2(5f, 5f), new Vector2(11.75f, 5f), 0f);

        clock.Tick(2f);

        Assert.False(clock.MoveDue(new Vector2(5f, 5f), 0f, East));
    }

    [Fact]
    public void TheLeadIsOneAndAHalfSecondsOfTravelAlongTheFacing()
    {
        Assert.Equal(new Vector2(16.75f, 10f), MoveSendClock.Lead(new Vector2(10f, 10f), East, 4.5f));
        Assert.Equal(new Vector2(10f, 7.75f), MoveSendClock.Lead(new Vector2(10f, 10f), North, -1.5f));
    }

    [Fact]
    public void ARotationWaitsFourSecondsAfterTheLastOne()
    {
        var clock = new MoveSendClock();
        clock.Rotated();

        clock.Tick(3.9f);
        Assert.False(clock.RotateDue(90f, 0f));

        clock.Tick(0.2f);
        Assert.True(clock.RotateDue(90f, 0f));
        Assert.False(clock.RotateDue(0f, 0f));
    }
}
