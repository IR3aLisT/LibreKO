using System.Collections.Concurrent;
using LibreKO.Game.Configuration;

namespace LibreKO.Game.World;

public static class ScheduleTime
{
    private static readonly ConcurrentDictionary<string, TimeZoneInfo?> Zones = new();

    public static bool TryZone(string? id, out TimeZoneInfo zone)
    {
        zone = TimeZoneInfo.Utc;
        if (string.IsNullOrWhiteSpace(id))
            return true;
        if (Zones.GetOrAdd(id, Find) is not { } found)
            return false;
        zone = found;
        return true;
    }

    public static TimeZoneInfo Zone(EventSettings events)
    {
        TryZone(events.ScheduleTimeZone, out var zone);
        return zone;
    }

    public static DateTime Now(EventSettings events, DateTime utcNow) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), Zone(events));

    public static int OffsetMinutes(EventSettings events, DateTime utcNow) =>
        (int)Math.Round(Zone(events).GetUtcOffset(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc)).TotalMinutes);

    private static TimeZoneInfo? Find(string id)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return null;
        }
    }
}
