namespace LibreKO.Domain;

public static class OnlineTime
{
    private const long SecondsPerMinute = 60;
    private const long SecondsPerHour = 3600;

    public static string Format(long seconds)
    {
        if (seconds < 0) seconds = 0;
        if (seconds >= SecondsPerHour)
            return $"{seconds / SecondsPerHour} h {seconds % SecondsPerHour / SecondsPerMinute} min";
        return seconds >= SecondsPerMinute ? $"{seconds / SecondsPerMinute} min" : $"{seconds} s";
    }
}
