using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.World;

namespace LibreKO.Game.Protocol.Writers;

public static class ExpSealPacketWriter
{
    public static Packet Toggle(byte sub, byte result)
    {
        var packet = new Packet(GameOpcodes.GS_EXP_SEAL);
        packet.WriteByte(sub);
        packet.WriteByte(result);
        return packet;
    }

    public static Packet Stored(long total)
    {
        var packet = Code(ExpSealRules.SubStore, ExpSealRules.Succeeded);
        packet.WriteLong(total);
        return packet;
    }

    public static Packet Total(long total)
    {
        var packet = new Packet(GameOpcodes.GS_EXP_SEAL);
        packet.WriteByte(ExpSealRules.SubTotal);
        packet.WriteLong(total);
        return packet;
    }

    public static Packet Code(byte sub, short code)
    {
        var packet = new Packet(GameOpcodes.GS_EXP_SEAL);
        packet.WriteByte(sub);
        packet.WriteShort(code);
        return packet;
    }
}
