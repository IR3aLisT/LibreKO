using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class TempleJoinTests
{
    [Theory]
    [InlineData(TempleJoin.AlreadyJoined, 17999)]
    [InlineData(TempleJoin.NeedsMapOfChaos, 18801)]
    [InlineData(TempleJoin.LadderPoints, 41005)]
    [InlineData(TempleJoin.ApplyLadderPoints, 41005)]
    [InlineData(TempleJoin.LevelTooLow, 44543)]
    [InlineData(TempleJoin.DuplicateIp, 44422)]
    [InlineData(TempleJoin.NotOpen, 44545)]
    [InlineData(TempleJoin.Joined, 0)]
    [InlineData(TempleJoin.LevelTooHigh, 0)]
    public void ResultCodesUseTheirTexts(byte result, int text) =>
        Assert.Equal(text, TempleJoin.TextId(result));

    [Fact]
    public void BothJoinedAndAlreadyJoinedLeaveYouSignedUp()
    {
        Assert.True(TempleJoin.SignedUp(TempleJoin.Joined));
        Assert.True(TempleJoin.SignedUp(TempleJoin.AlreadyJoined));
        Assert.False(TempleJoin.SignedUp(TempleJoin.NotOpen));
    }

    [Fact]
    public void ChaosCountsEveryoneTogether()
    {
        Assert.Equal("Signed up: 5", TempleJoin.CountLine(true, 2, 3));
        Assert.Equal("Karus 2  ·  El Morad 3", TempleJoin.CountLine(false, 2, 3));
    }
}
