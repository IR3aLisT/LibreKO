using System;
using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class BorderWarStateTests
{
    private static readonly DateTime Now = new(2026, 10, 2, 20, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void TheRoomClockRunsUntilTheFinish()
    {
        var war = new BorderWarState();
        war.RoomClock(1800, Now);
        Assert.Equal(1800, war.EndSecondsLeft(Now));
        Assert.Equal(1790, war.EndSecondsLeft(Now.AddSeconds(10)));
        war.Finish(1, 20, Now.AddSeconds(30));
        Assert.Equal(0, war.EndSecondsLeft(Now.AddSeconds(31)));
        war.Reset();
        Assert.Null(war.EndUtc);
    }

    [Theory]
    [InlineData(1, 1, WarOutcome.Victory)]
    [InlineData(2, 1, WarOutcome.Defeat)]
    [InlineData(0, 2, WarOutcome.Draw)]
    public void TheOutcomeFollowsTheWinner(int winner, int myNation, WarOutcome expected)
    {
        Assert.Equal(expected, BorderWarState.OutcomeFor(winner, myNation));
    }

    [Fact]
    public void TakingTheFragmentStopsTheAltarCountdown()
    {
        var war = new BorderWarState();
        war.AltarTimer(46, Now);
        war.FragmentTaken("Zeus", 1);
        Assert.Equal("Zeus", war.Carrier);
        Assert.Equal(0, war.AltarSecondsLeft(Now));
    }

    [Fact]
    public void TheAltarCountdownRunsOnTheClock()
    {
        var war = new BorderWarState();
        war.FragmentTaken("Zeus", 1);
        war.AltarTimer(46, Now);
        Assert.Equal("", war.Carrier);
        Assert.Equal(46, war.AltarSecondsLeft(Now));
        Assert.Equal(36, war.AltarSecondsLeft(Now.AddSeconds(10)));
        Assert.Equal(0, war.AltarSecondsLeft(Now.AddMinutes(5)));
    }

    [Fact]
    public void AZeroAltarTimerMeansTheAltarIsBack()
    {
        var war = new BorderWarState();
        war.AltarTimer(46, Now);
        war.AltarTimer(0, Now.AddSeconds(46));
        Assert.Null(war.AltarBackUtc);
    }

    [Fact]
    public void TheFinishKeepsTheScoresAndCountsDownToTheTripHome()
    {
        var war = new BorderWarState();
        war.SetScores(320, 510);
        war.FragmentTaken("Zeus", 1);
        war.Finish(2, 60, Now);
        Assert.True(war.Finished);
        Assert.Equal(2, war.Winner);
        Assert.Equal(510, war.ElmoradScore);
        Assert.Equal("", war.Carrier);
        Assert.Equal(30, war.HomeSecondsLeft(Now.AddSeconds(30)));
    }

    [Fact]
    public void ResetClearsEverything()
    {
        var war = new BorderWarState();
        war.SetScores(3, 4);
        war.Finish(0, 60, Now);
        war.Reset();
        Assert.False(war.Finished);
        Assert.Equal(0, war.KarusScore);
        Assert.Null(war.HomeUtc);
    }
}
