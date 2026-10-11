using LibreKO.Common.Infrastructure.Network;

namespace LibreKO.Game.Protocol.Writers;

public sealed class ClanBroadcastPacketWriter
{
    public static Packet ClanPremium(byte sub, byte result, uint secondsLeft)
    {
        var packet = new Packet(GameOpcodes.GS_CLAN_PREMIUM);
        packet.WriteByte(sub);
        packet.WriteByte(result);
        packet.WriteUInt(secondsLeft);
        return packet;
    }

    public static Packet ClanBattleNotification() => new(GameOpcodes.GS_CLAN_BATTLE);
}
