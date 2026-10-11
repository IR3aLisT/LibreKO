using LibreKO.Network;
using Xunit;

namespace LibreKO.Tests;

public class ClanPremiumReplyTests
{
    [Fact]
    public void AnActivePremiumCarriesItsSecondsLeft()
    {
        var p = new Packet(GameOpcodes.GS_CLAN_PREMIUM);
        p.WriteByte(1);
        p.WriteByte(1);
        p.WriteUInt(259_200);
        p.ResetOffset();

        var reply = ClanPremiumReply.Read(p);

        Assert.NotNull(reply);
        Assert.True(reply.Value.Active);
        Assert.Equal(259_200u, reply.Value.SecondsLeft);
    }

    [Fact]
    public void AnOlderReplyWithoutTheTimeStillReads()
    {
        var p = new Packet(GameOpcodes.GS_CLAN_PREMIUM);
        p.WriteByte(2);
        p.WriteByte(0);
        p.ResetOffset();

        var reply = ClanPremiumReply.Read(p);

        Assert.NotNull(reply);
        Assert.False(reply.Value.Active);
        Assert.Equal(0u, reply.Value.SecondsLeft);
    }
}
