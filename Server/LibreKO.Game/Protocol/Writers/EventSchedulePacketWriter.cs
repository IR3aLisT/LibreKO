using LibreKO.Common.Infrastructure.Network;

namespace LibreKO.Game.Protocol.Writers;

public enum EventScheduleKind : byte
{
    Temple = 1,
    CollectionRace = 2,
    Lottery = 3,
}

public enum EventSchedulePhase : byte
{
    SignUp = 1,
    Running = 2,
}

public readonly record struct EventNow(
    EventScheduleKind Kind, string Name, EventSchedulePhase Phase, int SecondsLeft, byte MinLevel, byte MaxLevel);

public readonly record struct EventSlot(
    EventScheduleKind Kind, string Name, byte Day, byte HourUtc, byte MinuteUtc, byte MinLevel, byte MaxLevel);

public static class EventSchedulePacketWriter
{
    public const byte SubSchedule = 1;
    public const byte EveryDay = 7;

    public static Packet Schedule(IReadOnlyList<EventNow> now, IReadOnlyList<EventSlot> slots)
    {
        var packet = new Packet(GameOpcodes.GS_EVENT_SCHEDULE);
        packet.WriteByte(SubSchedule);
        packet.WriteByte((byte)Math.Min(now.Count, byte.MaxValue));
        foreach (var entry in now.Take(byte.MaxValue))
        {
            packet.WriteByte((byte)entry.Kind);
            packet.WriteString(entry.Name);
            packet.WriteByte((byte)entry.Phase);
            packet.WriteInt(entry.SecondsLeft);
            packet.WriteByte(entry.MinLevel);
            packet.WriteByte(entry.MaxLevel);
        }

        packet.WriteUShort((ushort)Math.Min(slots.Count, ushort.MaxValue));
        foreach (var slot in slots.Take(ushort.MaxValue))
        {
            packet.WriteByte((byte)slot.Kind);
            packet.WriteString(slot.Name);
            packet.WriteByte(slot.Day);
            packet.WriteByte(slot.HourUtc);
            packet.WriteByte(slot.MinuteUtc);
            packet.WriteByte(slot.MinLevel);
            packet.WriteByte(slot.MaxLevel);
        }

        return packet;
    }
}
