using LibreKO.Common.Infrastructure.Network;

namespace LibreKO.Game.Protocol.Writers;

public static class ZoneTerrainPacketWriter
{
    public const byte SubTerrain = 1;
    public const byte MaxEffect = 4;

    public static Packet Effect(byte effect)
    {
        var packet = new Packet(GameOpcodes.GS_ZONE_TERRAIN);
        packet.WriteByte(SubTerrain);
        packet.WriteByte(effect);
        return packet;
    }
}
