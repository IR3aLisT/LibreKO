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
    private const int HeaderBytes = 1 + 4 + 1;
    private const int NowFixedBytes = 1 + 1 + 2 + 1 + 4 + 1 + 1;
    private const int EventFixedBytes = 1 + 2 + 2 + 1 + 1 + 1;
    private const int SlotBytes = 1 + 1 + 1;

    public static EventSchedule? Read(Packet p)
    {
        if (p.RemainingBytes < HeaderBytes || p.ReadByte() != SubSchedule) return null;
        var clockOffset = TimeSpan.FromSeconds(p.ReadInt());
        int nowCount = p.ReadByte();
        var now = new List<EventNow>(nowCount);
        for (int i = 0; i < nowCount && p.RemainingBytes >= NowFixedBytes; i++)
        {
            var kind = (EventKind)p.ReadByte();
            int code = p.ReadByte();
            string name = p.ReadString();
            var phase = (EventPhase)p.ReadByte();
            int seconds = p.ReadInt();
            now.Add(new EventNow(kind, code, name, phase, seconds, p.ReadByte(), p.ReadByte()));
        }

        var events = new List<ScheduledEvent>();
        if (p.RemainingBytes >= 2)
        {
            int count = p.ReadUShort();
            for (int i = 0; i < count && p.RemainingBytes >= EventFixedBytes; i++)
            {
                var kind = (EventKind)p.ReadByte();
                string name = p.ReadString();
                string description = p.ReadString();
                int minLevel = p.ReadByte();
                int maxLevel = p.ReadByte();
                int slotCount = p.ReadByte();
                var slots = new List<EventSlot>(slotCount);
                for (int s = 0; s < slotCount && p.RemainingBytes >= SlotBytes; s++)
                    slots.Add(new EventSlot(p.ReadByte(), p.ReadByte(), p.ReadByte()));
                events.Add(new ScheduledEvent(kind, name, description, minLevel, maxLevel, slots));
            }
        }

        return new EventSchedule(clockOffset, now, events, DateTime.UtcNow);
    }
}
