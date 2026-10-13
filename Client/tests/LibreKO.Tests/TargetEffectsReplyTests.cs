using LibreKO.Network;
using Xunit;

namespace LibreKO.Tests;

public class TargetEffectsReplyTests
{
    [Fact]
    public void TheEffectsListCarriesSkillsAndSecondsInOrder()
    {
        var p = new Packet(GameOpcodes.GS_TARGET_STATE);
        p.WriteInt(802);
        p.WriteByte(2);
        p.WriteInt(107725);
        p.WriteUShort(30);
        p.WriteInt(109756);
        p.WriteUShort(65000);
        p.ResetOffset();

        var reply = TargetEffectsReply.Read(p);

        Assert.NotNull(reply);
        Assert.Equal(802, reply.Value.TargetId);
        Assert.Equal(2, reply.Value.Effects.Count);
        Assert.Equal(new TargetEffect(107725, 30), reply.Value.Effects[0]);
        Assert.Equal(new TargetEffect(109756, 65000), reply.Value.Effects[1]);
        Assert.Equal(0, p.RemainingBytes);
    }

    [Fact]
    public void AnEmptyListIsStillAReply()
    {
        var p = new Packet(GameOpcodes.GS_TARGET_STATE);
        p.WriteInt(12001);
        p.WriteByte(0);
        p.ResetOffset();

        var reply = TargetEffectsReply.Read(p);

        Assert.NotNull(reply);
        Assert.Empty(reply.Value.Effects);
    }
}
