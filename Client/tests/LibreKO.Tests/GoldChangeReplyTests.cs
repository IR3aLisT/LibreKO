using LibreKO.Network;
using Xunit;

namespace LibreKO.Tests;

public class GoldChangeReplyTests
{
    [Fact]
    public void TheSubAmountAndTotalAreRead()
    {
        var p = new Packet(GameOpcodes.GS_GOLD_CHANGE);
        p.WriteByte(GoldChangeReply.Lost);
        p.WriteInt(2500);
        p.WriteInt(97500);
        p.ResetOffset();

        var reply = GoldChangeReply.Read(p);

        Assert.Equal(new GoldChangeReply(GoldChangeReply.Lost, 2500, 97500), reply);
        Assert.Equal(0, p.RemainingBytes);
    }

    [Fact]
    public void AShortPacketIsIgnored()
    {
        var p = new Packet(GameOpcodes.GS_GOLD_CHANGE);
        p.WriteByte(GoldChangeReply.Earned);
        p.ResetOffset();

        Assert.Null(GoldChangeReply.Read(p));
    }
}
