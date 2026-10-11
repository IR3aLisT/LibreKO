using LibreKO.Common.Infrastructure.Network;

namespace LibreKO.Game.Protocol.Writers;

public static class SoccerPacketWriter
{
    public const byte MiningSub = 16;
    public const byte SubGoal = 1;
    public const byte SubClock = 2;
    public const byte SubSync = 3;
    public const byte SubEnd = 4;
    private const byte SyncPadding = 0;

    public static Packet Goal(int kickerId, byte kickerTeam, byte goalTeam, byte blue, byte red)
    {
        var packet = Sub(SubGoal);
        packet.WriteInt(kickerId);
        packet.WriteByte(kickerTeam);
        packet.WriteByte(goalTeam);
        packet.WriteByte(blue);
        packet.WriteByte(red);
        return packet;
    }

    public static Packet Clock(int seconds)
    {
        var packet = Sub(SubClock);
        packet.WriteShort((short)Math.Clamp(seconds, 0, short.MaxValue));
        return packet;
    }

    public static Packet Sync(int seconds, byte blue, byte red)
    {
        var packet = Sub(SubSync);
        packet.WriteByte(SyncPadding);
        packet.WriteShort((short)Math.Clamp(seconds, 0, short.MaxValue));
        packet.WriteByte(blue);
        packet.WriteByte(red);
        return packet;
    }

    public static Packet End(byte winner, byte blue, byte red)
    {
        var packet = Sub(SubEnd);
        packet.WriteByte(winner);
        packet.WriteByte(blue);
        packet.WriteByte(red);
        return packet;
    }

    private static Packet Sub(byte sub)
    {
        var packet = new Packet(GameOpcodes.GS_MINING);
        packet.WriteByte(MiningSub);
        packet.WriteByte(sub);
        return packet;
    }
}
