using LibreKO.Network;
using Xunit;

namespace LibreKO.Tests;

public class CommanderRepliesTests
{
    [Fact]
    public void TheNewCommanderComesWithTheirName()
    {
        var p = new Packet(GameOpcodes.GS_PARTY);
        p.WriteInt(70001);
        p.WriteString("Aurelia");
        p.ResetOffset();

        var reply = CommanderNamedReply.Read(p);

        Assert.NotNull(reply);
        Assert.Equal(70001, reply.Value.CharId);
        Assert.Equal("Aurelia", reply.Value.Name);
        Assert.Equal(0, p.RemainingBytes);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public void TheMarkCarriesTheTargetAndWhetherItClears(byte flag, bool clear)
    {
        var p = new Packet(GameOpcodes.GS_PARTY);
        p.WriteInt(7301);
        p.WriteByte(flag);
        p.ResetOffset();

        var reply = CommanderMarkReply.Read(p);

        Assert.NotNull(reply);
        Assert.Equal(7301, reply.Value.TargetId);
        Assert.Equal(clear, reply.Value.Clear);
    }
}
