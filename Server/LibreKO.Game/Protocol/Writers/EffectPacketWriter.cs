using LibreKO.Common.Infrastructure.Network;

namespace LibreKO.Game.Protocol.Writers;

public static class EffectPacketWriter
{
    public const int AnnouncementEntity = -1;

    public static Packet ItemAnnouncement(int itemId, string winner)
    {
        var packet = new Packet(GameOpcodes.GS_EFFECT);
        packet.WriteInt(AnnouncementEntity);
        packet.WriteInt(itemId);
        packet.WriteSByteString(winner);
        return packet;
    }
}
