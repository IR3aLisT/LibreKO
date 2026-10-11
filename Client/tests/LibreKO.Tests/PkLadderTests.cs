using LibreKO.Domain;
using LibreKO.Network;
using Xunit;

namespace LibreKO.Tests;

public class PkLadderTests
{
    [Fact]
    public void BothNationsAreReadWithTheirBonusAndSymbolThenTheOwnRow()
    {
        var p = new Packet(GameOpcodes.GS_RANK);
        p.WriteUShort(1);
        p.WriteString("Velkan");
        p.WriteByte(1);
        p.WriteUShort(42);
        p.WriteUShort(7);
        p.WriteString("Wolves");
        p.WriteInt(12_480);
        p.WriteUShort(120);
        p.WriteByte(3);
        p.WriteUShort(0);
        p.WriteUShort(4);
        p.WriteInt(1_870);
        p.WriteUShort(60);
        p.ResetOffset();

        var ladder = PkLadderReply.Read(p);

        var top = Assert.Single(ladder.Karus);
        Assert.Equal(("Velkan", "Wolves", 12_480, 120, 3, 1), (top.Name, top.ClanName, top.Loyalty, top.Bonus, top.Symbol, top.Place));
        Assert.Empty(ladder.ElMorad);
        Assert.Equal((4, 1_870, 60), (ladder.MyRank, ladder.MyPoints, ladder.MyBonus));
        Assert.Equal(0, p.RemainingBytes);
    }

    [Theory]
    [InlineData(71, true)]
    [InlineData(72, true)]
    [InlineData(73, true)]
    [InlineData(21, false)]
    public void TheLadderBelongsToTheThreePkZones(int zone, bool shown) =>
        Assert.Equal(shown, PkLadder.IsLadderZone(zone));

    [Fact]
    public void DataOlderThanTenSecondsIsAskedForAgain()
    {
        Assert.False(PkLadder.IsFresh(0, 5));
        Assert.True(PkLadder.IsFresh(100, 110));
        Assert.False(PkLadder.IsFresh(100, 110.5));
    }

    [Fact]
    public void TheBonusShowsOnlyWhenThereIsOne()
    {
        Assert.Equal("", PkLadder.Bonus(0));
        Assert.Equal("(+1,200)", PkLadder.Bonus(1_200));
        Assert.Equal("-", PkLadder.Place(0));
    }
}
