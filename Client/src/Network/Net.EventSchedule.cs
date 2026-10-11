using System;
using System.Collections.Generic;
using LibreKO.Domain;

namespace LibreKO.Network;

public partial class Net
{
    private const byte EventScheduleSub = 1;

    public event Action<EventSchedule>? EventScheduleEvent;

    public void SendEventScheduleRequest()
    {
        var p = new Packet(GameOpcodes.GS_EVENT_SCHEDULE);
        p.WriteByte(EventScheduleSub);
        _conn.Send(p);
    }

    private void HandleEventSchedule(Packet p)
    {
        if (EventScheduleReader.Read(p) is { } schedule) EventScheduleEvent?.Invoke(schedule);
    }
}

public static class EventScheduleReader
{
    private const byte SubSchedule = 1;
    private const int NowFixedBytes = 1 + 2 + 1 + 4 + 1 + 1;
    private const int SlotFixedBytes = 1 + 2 + 1 + 1 + 1 + 1 + 1;

    public static EventSchedule? Read(Packet p)
    {
        if (p.RemainingBytes < 2 || p.ReadByte() != SubSchedule) return null;
        int nowCount = p.ReadByte();
        var now = new List<EventNow>(nowCount);
        for (int i = 0; i < nowCount && p.RemainingBytes >= NowFixedBytes; i++)
        {
            var kind = (EventKind)p.ReadByte();
            string name = p.ReadString();
            var phase = (EventPhase)p.ReadByte();
            int seconds = p.ReadInt();
            now.Add(new EventNow(kind, name, phase, seconds, p.ReadByte(), p.ReadByte()));
        }

        var slots = new List<EventSlot>();
        if (p.RemainingBytes >= 2)
        {
            int count = p.ReadUShort();
            for (int i = 0; i < count && p.RemainingBytes >= SlotFixedBytes; i++)
            {
                var kind = (EventKind)p.ReadByte();
                string name = p.ReadString();
                slots.Add(new EventSlot(kind, name, p.ReadByte(), p.ReadByte(), p.ReadByte(), p.ReadByte(), p.ReadByte()));
            }
        }

        return new EventSchedule(now, slots, DateTime.UtcNow);
    }
}
