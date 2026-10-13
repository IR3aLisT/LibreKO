using System.Collections.Generic;

namespace LibreKO.Domain;

public static class PremiumNames
{
    public const int PremiumText = 1490;
    public const string PremiumFallback = "PREMIUM";
    public const int HoursText = 7630;
    public const string HoursFallback = "Premium Service : %d hours remaining.";
    public const int DaysText = 43550;
    public const string DaysFallback = "Premium Service : %d day(s) remaining.";
    public const int LastDayText = 7631;
    public const string LastDayFallback = "Today is the last day of premium service.";
    private const int HoursPerDay = 24;

    private static readonly Dictionary<int, (int Text, string Fallback)> Names = new()
    {
        [1] = (1479, "NORMAL"), [2] = (1480, "ULTRA"), [3] = (1481, "BRONZE"), [5] = (1483, "GOLD"),
        [7] = (1485, "PLATINUM"), [8] = (1486, "ROYAL"), [10] = (1487, "DC"), [11] = (1488, "EXP"), [12] = (1489, "WAR"),
    };

    public static (int Text, string Fallback)? Name(int type) => Names.TryGetValue(type, out var name) ? name : null;

    public static (int Text, string Fallback, int Value) TimeLeft(int hours) =>
        hours <= 0 ? (LastDayText, LastDayFallback, 0)
        : hours < HoursPerDay ? (HoursText, HoursFallback, hours)
        : (DaysText, DaysFallback, hours / HoursPerDay);
}
