using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class RankBoardTests
{
    [Theory]
    [InlineData(1000, RankBoardKind.King, 0)]
    [InlineData(1101, RankBoardKind.Player, 0)]
    [InlineData(1110, RankBoardKind.Player, 9)]
    [InlineData(1201, RankBoardKind.Clan, 0)]
    [InlineData(1210, RankBoardKind.Clan, 9)]
    [InlineData(2011, RankBoardKind.CommanderKiller, 0)]
    [InlineData(2013, RankBoardKind.CommanderKiller, 2)]
    [InlineData(2311, RankBoardKind.TopKiller, 0)]
    [InlineData(2319, RankBoardKind.TopKiller, 2)]
    public void SlotsLandWhere2619PutsThem(int slot, RankBoardKind kind, int index) =>
        Assert.Equal((kind, index), RankBoard.Classify(slot));

    [Fact]
    public void SlotsPastTheLastGroupAreDropped() => Assert.Null(RankBoard.Classify(1500));

    [Fact]
    public void AColumnKeepsEachRowAtItsPlace()
    {
        var rows = new[]
        {
            new RankBoardRow(RankBoardKind.Player, 2, "Third", 0, 0, "", 0),
            new RankBoardRow(RankBoardKind.Clan, 0, "Chief", 4, 1, "Wolves", 0),
        };

        var players = RankBoard.Column(rows, RankBoardKind.Player, RankBoard.Rows);

        Assert.Null(players[0]);
        Assert.Equal("Third", players[2]?.Name);
    }
}
