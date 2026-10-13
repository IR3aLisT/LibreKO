using LibreKO.Common.Infrastructure.Network;

namespace LibreKO.Game.Protocol.Writers;

public static class KillAssistPacketWriter
{
    public const byte KillSub = 1;
    public const byte AssistSub = 2;

    public const byte OwnStreak = 1;
    public const byte OtherStreak = 2;
    public const byte StreakAnnouncement = 3;

    public static Packet Kill(byte kind, int characterId, string name, byte nation, ushort count)
    {
        var packet = new Packet(GameOpcodes.GS_KILL_ASSIST);
        packet.WriteByte(KillSub);
        packet.WriteByte(kind);
        packet.WriteInt(characterId);
        packet.WriteString(name);
        packet.WriteByte(nation);
        packet.WriteUShort(count);
        return packet;
    }

    public static Packet Assist(int characterId, ushort count, byte grade)
    {
        var packet = new Packet(GameOpcodes.GS_KILL_ASSIST);
        packet.WriteByte(AssistSub);
        packet.WriteInt(characterId);
        packet.WriteUShort(count);
        packet.WriteByte(grade);
        return packet;
    }
}
