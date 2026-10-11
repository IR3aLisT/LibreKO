using LibreKO.Network;
using Xunit;

namespace LibreKO.Tests;

public class RegeneReplyTests
{
    private static Packet Header(ushort x, ushort z, byte type)
    {
        var p = new Packet(GameOpcodes.GS_REGENE);
        p.WriteUShort(x);
        p.WriteUShort(z);
        p.WriteUShort(0);
        p.WriteByte(type);
        return p;
    }

    [Fact]
    public void ARespawnCarriesThePositionPastTheHeight()
    {
        var p = Header(35000, 1200, RegeneReply.Respawned);
        p.ResetOffset();

        var reply = RegeneReply.Read(p);

        Assert.NotNull(reply);
        Assert.Equal(RegeneReply.Respawned, reply.Value.Type);
        Assert.Equal(3500f, reply.Value.X);
        Assert.Equal(120f, reply.Value.Z);
        Assert.Equal(0, p.RemainingBytes);
    }

    [Fact]
    public void AnOfferNamesTheCasterAndTheSkill()
    {
        var p = Header(1000, 1000, RegeneReply.Offer);
        p.WriteInt(701);
        p.WriteInt(111545);
        p.ResetOffset();

        var reply = RegeneReply.Read(p);

        Assert.NotNull(reply);
        Assert.Equal(701, reply.Value.CasterId);
        Assert.Equal(111545, reply.Value.SkillId);
    }

    [Fact]
    public void AMissingItemRefusalCarriesTheItemAndTheAmount()
    {
        var p = Header(1000, 1000, RegeneReply.Refused);
        p.WriteUShort(RegeneReply.RefusedMissingItem);
        p.WriteInt(379006000);
        p.WriteUShort(180);
        p.ResetOffset();

        var reply = RegeneReply.Read(p);

        Assert.NotNull(reply);
        Assert.Equal(RegeneReply.RefusedMissingItem, reply.Value.Code);
        Assert.Equal(379006000, reply.Value.ItemId);
        Assert.Equal(180, reply.Value.Amount);
        Assert.Equal(0, p.RemainingBytes);
    }

    [Fact]
    public void AnOtherRefusalCarriesOnlyItsCode()
    {
        var p = Header(1000, 1000, RegeneReply.Refused);
        p.WriteUShort(RegeneReply.RefusedStandby);
        p.ResetOffset();

        var reply = RegeneReply.Read(p);

        Assert.NotNull(reply);
        Assert.Equal(RegeneReply.RefusedStandby, reply.Value.Code);
    }
}
