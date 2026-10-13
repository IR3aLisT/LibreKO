using LibreKO.Common.Infrastructure.Network;

namespace LibreKO.Game.Protocol.Writers;

public static class InstanceDashboardPacketWriter
{
    public const byte DashboardStyle = 7;
    public const uint EventClockSub = 9;

    public const byte JuraidWarningStyle = 67;

    public static Packet JuraidWarning(short warnings, short limit)
    {
        var packet = new Packet(GameOpcodes.GS_SELECT_MSG);
        packet.WriteInt(0);
        packet.WriteByte(JuraidWarningStyle);
        packet.WriteInt(warnings);
        packet.WriteInt(limit);
        return packet;
    }

    public static Packet EventClock(byte eventId, uint secondsLeft)
    {
        var packet = new Packet(GameOpcodes.GS_SELECT_MSG);
        packet.WriteInt(0);
        packet.WriteByte(DashboardStyle);
        packet.WriteLong(0);
        packet.WriteUInt(EventClockSub);
        packet.WriteByte(eventId);
        packet.WriteUInt(secondsLeft);
        return packet;
    }
}
