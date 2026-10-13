using LibreKO.Network;
using Xunit;

namespace LibreKO.Tests;

public class TargetQuestDropTests
{
    [Fact]
    public void TheRowsAfterTheHpFieldsReadBack()
    {
        var p = new Packet(GameOpcodes.GS_TARGET_HP);
        p.WriteByte(2);
        p.WriteInt(379273000);
        p.WriteShort(2);
        p.WriteShort(3);
        p.WriteInt(379274000);
        p.WriteShort(0);
        p.WriteShort(5);
        p.ResetOffset();

        var rows = TargetQuestDrop.ReadList(p);

        Assert.Equal(new[] { new TargetQuestDrop(379273000, 2, 3), new TargetQuestDrop(379274000, 0, 5) }, rows);
        Assert.Equal("Animal Blood: 2/3", rows[0].Line("Animal Blood"));
    }

    [Fact]
    public void APacketWithoutRowsHasNone()
    {
        var p = new Packet(GameOpcodes.GS_TARGET_HP);
        p.ResetOffset();

        Assert.Empty(TargetQuestDrop.ReadList(p));
    }
}
