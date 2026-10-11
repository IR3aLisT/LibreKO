using LibreKO.Network;
using Xunit;

namespace LibreKO.Tests;

public class ContinuousPacketTests
{
    [Fact]
    public void ABundleSplitsIntoItsPacketsInOrder()
    {
        var bundle = new Packet(GameOpcodes.GS_CONTINOUS_PACKET);
        byte[] first = [(byte)GameOpcodes.GS_TIME, 1, 2];
        byte[] second = [(byte)GameOpcodes.GS_WEATHER, 3];
        bundle.WriteShort((short)(2 + 2 + first.Length + 2 + second.Length));
        bundle.WriteShort((short)first.Length);
        bundle.WriteBytes(first);
        bundle.WriteShort((short)second.Length);
        bundle.WriteBytes(second);
        bundle.ResetOffset();

        var pieces = ContinuousPacket.Split(bundle);

        Assert.Equal(2, pieces.Count);
        Assert.Equal((byte)GameOpcodes.GS_TIME, pieces[0].GetOpcode());
        Assert.Equal(1, pieces[0].ReadByte());
        Assert.Equal((byte)GameOpcodes.GS_WEATHER, pieces[1].GetOpcode());
        Assert.Equal(3, pieces[1].ReadByte());
    }

    [Fact]
    public void ABadLengthStopsTheSplit()
    {
        var bundle = new Packet(GameOpcodes.GS_CONTINOUS_PACKET);
        bundle.WriteShort(20);
        bundle.WriteShort(0);
        bundle.ResetOffset();

        Assert.Empty(ContinuousPacket.Split(bundle));
    }
}
