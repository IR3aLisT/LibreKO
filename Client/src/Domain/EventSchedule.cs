using System;
using System.Collections.Generic;

namespace LibreKO.Domain;

public enum EventKind : byte { Temple = 1, CollectionRace = 2, Lottery = 3 }

public enum EventPhase : byte { SignUp = 1, Running = 2 }

public readonly record struct EventNow(EventKind Kind, string Name, EventPhase Phase, int SecondsLeft, int MinLevel, int MaxLevel);

public readonly record struct EventSlot(EventKind Kind, string Name, int Day, int Hour, int Minute, int MinLevel, int MaxLevel);

public readonly record struct UpcomingEvent(EventSlot Slot, DateTime StartUtc);

public sealed class EventSchedule
{
    public const int EveryDay = 7;
    private const int DaysPerWeek = 7;

    public IReadOnlyList<EventNow> Now { get; }
    public IReadOnlyList<EventSlot> Slots { get; }
    public DateTime ReceivedUtc { get; }

    public EventSchedule(IReadOnlyList<EventNow> now, IReadOnlyList<EventSlot> slots, DateTime receivedUtc)
    {
        Now = now;
        Slots = slots;
        ReceivedUtc = receivedUtc;
    }

    public int SecondsLeft(EventNow entry, DateTime utcNow) =>
        Math.Max(0, entry.SecondsLeft - (int)(utcNow - ReceivedUtc).TotalSeconds);

    public static bool Fits(int level, int minLevel, int maxLevel) =>
        (minLevel <= 0 || level >= minLevel) && (maxLevel <= 0 || level <= maxLevel);

    public static DateTime NextStartUtc(EventSlot slot, DateTime utcNow)
    {
        var today = utcNow.Date.AddHours(slot.Hour).AddMinutes(slot.Minute);
        if (slot.Day == EveryDay)
            return today >= utcNow ? today : today.AddDays(1);
        int ahead = ((slot.Day - (int)utcNow.DayOfWeek) % DaysPerWeek + DaysPerWeek) % DaysPerWeek;
        var start = today.AddDays(ahead);
        return start >= utcNow ? start : start.AddDays(DaysPerWeek);
    }

    public List<UpcomingEvent> Upcoming(DateTime utcNow, TimeSpan window)
    {
        var list = new List<UpcomingEvent>();
        foreach (var slot in Slots)
        {
            var start = NextStartUtc(slot, utcNow);
            if (start - utcNow < window) list.Add(new UpcomingEvent(slot, start));
        }
        list.Sort((a, b) => a.StartUtc.CompareTo(b.StartUtc));
        return list;
    }

    public bool AlertDue(int level, DateTime utcNow, TimeSpan lead)
    {
        foreach (var slot in Slots)
        {
            if (!Fits(level, slot.MinLevel, slot.MaxLevel)) continue;
            if (NextStartUtc(slot, utcNow) - utcNow <= lead) return true;
        }
        return false;
    }
}
