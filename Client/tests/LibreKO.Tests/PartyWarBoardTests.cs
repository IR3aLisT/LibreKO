using LibreKO.Domain;
using LibreKO.Network;
using Xunit;

namespace LibreKO.Tests;

public class PartyWarBoardTests
{
    [Fact]
    public void TheReplyReadsTargetModeAndShares()
    {
        var p = new Packet(GameOpcodes.GS_PARTY);
        p.WriteByte(PartyWarBoard.TargetBoard);
        p.WriteByte(PartyWarBoard.ModeHealing);
        p.WriteByte(2);
        p.WriteInt(11);
        p.WriteShort(750);
        p.WriteInt(12);
        p.WriteShort(250);
        p.ResetOffset();

        var reply = Net.ReadWarBoard(p);

        Assert.NotNull(reply);
        Assert.Equal(PartyWarBoard.TargetBoard, reply.Value.Target);
        Assert.Equal(PartyWarBoard.ModeHealing, reply.Value.Mode);
        Assert.Equal(new[] { new WarBoardShare(11, 750), new WarBoardShare(12, 250) }, reply.Value.Shares);
    }

    [Theory]
    [InlineData(0, "0.0%")]
    [InlineData(5, "0.5%")]
    [InlineData(881, "88.1%")]
    [InlineData(1000, "100.0%")]
    public void PercentagesAreTenthsOfThousandths(int share, string text)
    {
        Assert.Equal(text, PartyWarBoard.Percent(share));
    }

    [Fact]
    public void TheRowToggleCyclesOffDamageHealing()
    {
        Assert.Equal(PartyWarBoard.ModeDamage, PartyWarBoard.NextRowMode(PartyWarBoard.ModeOff));
        Assert.Equal(PartyWarBoard.ModeHealing, PartyWarBoard.NextRowMode(PartyWarBoard.ModeDamage));
        Assert.Equal(PartyWarBoard.ModeOff, PartyWarBoard.NextRowMode(PartyWarBoard.ModeHealing));
    }

    [Fact]
    public void TheBoardRanksByShare()
    {
        var ranked = PartyWarBoard.Ranked(new[]
        {
            new WarBoardShare(1, 100), new WarBoardShare(2, 600), new WarBoardShare(3, 300),
        });

        Assert.Equal(new[] { 2, 3, 1 }, new[] { ranked[0].MemberId, ranked[1].MemberId, ranked[2].MemberId });
        Assert.Equal("1. Rin", PartyWarBoard.RowName(1, "Rin"));
    }

    [Fact]
    public void PriestsOpenTheBoardOnHealing()
    {
        Assert.Equal(PartyWarBoard.ModeHealing, PartyWarBoard.DefaultBoardMode(104));
        Assert.Equal(PartyWarBoard.ModeDamage, PartyWarBoard.DefaultBoardMode(201));
    }

    [Fact]
    public void TheLimitCountsDownFromTheLastSend()
    {
        Assert.Equal(3.0, PartyWarBoard.SecondsLeft(10, 12, PartyWarBoard.BoardTabSeconds));
        Assert.Equal(0.0, PartyWarBoard.SecondsLeft(10, 16, PartyWarBoard.BoardTabSeconds));
    }
}
