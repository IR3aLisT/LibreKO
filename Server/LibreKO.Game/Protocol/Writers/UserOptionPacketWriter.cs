using LibreKO.Common.Infrastructure.Network;

namespace LibreKO.Game.Protocol.Writers;

public static class UserOptionPacketWriter
{
    public const byte SubRankAura = 2;
    private const byte Shown = 1;
    private const byte Hidden = 0;

    public static Packet RankAura(int characterId, bool shown)
    {
        var packet = new Packet(GameOpcodes.GS_USER_OPTION);
        packet.WriteByte(SubRankAura);
        packet.WriteInt(characterId);
        packet.WriteByte(shown ? Shown : Hidden);
        return packet;
    }
}
