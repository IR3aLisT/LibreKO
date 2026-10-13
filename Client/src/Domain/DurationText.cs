using System;

namespace LibreKO.Domain;

public static class DurationText
{
    public static string Short(long seconds)
    {
        if (seconds <= 0) return "";
        var left = TimeSpan.FromSeconds(seconds);
        if (left.Days >= 1) return $"{left.Days}d {left.Hours}h";
        if (left.Hours >= 1) return $"{left.Hours}h {left.Minutes}m";
        return $"{Math.Max(1, left.Minutes)}m";
    }
}
