using LibreKO.Network;
using Xunit;

namespace LibreKO.Tests;

public class ClanBattleWireTests
{
    private static Packet Knights(System.Action<Packet> body)
    {
        var p = new Packet(GameOpcodes.GS_KNIGHTS_PROCESS);
        body(p);
        p.ResetOffset();
        return p;
    }

    [Fact]
    public void AChallengeReadsTheChallengerAndTheirName()
    {
        var p = Knights(w =>
        {
            w.WriteByte(Net.ClanBattleChallenged);
            w.WriteInt(1234);
            w.WriteUShort(0);
            w.WriteString("Alpha");
        });

        var reply = ClanBattleWire.ReadResult(p);

        Assert.Equal(new ClanBattleWire.ResultReply(Net.ClanBattleChallenged, 1234, "Alpha"), reply);
    }

    [Fact]
    public void ARefusalIsJustItsCode()
    {
        var p = Knights(w => w.WriteByte(4));

        Assert.Equal(new ClanBattleWire.ResultReply(4, 0, ""), ClanBattleWire.ReadResult(p));
    }

    [Fact]
    public void TheEndNamesTheOtherClanUnlessItOnlyClears()
    {
        var surrendered = Knights(w =>
        {
            w.WriteByte(Net.ClanBattleTheySurrendered);
            w.WriteShort(11);
            w.WriteUShort(0);
            w.WriteString("Doves");
        });
        var cleared = Knights(w => w.WriteByte(Net.ClanBattleCleared));

        Assert.Equal(new ClanBattleWire.EndReply(Net.ClanBattleTheySurrendered, "Doves"), ClanBattleWire.ReadEnd(surrendered));
        Assert.Equal(new ClanBattleWire.EndReply(Net.ClanBattleCleared, ""), ClanBattleWire.ReadEnd(cleared));
    }
}
