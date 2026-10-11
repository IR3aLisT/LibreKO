using System;
using System.Globalization;

namespace LibreKO.Domain;

public static class QuestJournalRules
{
    public const int AnywhereZone = 0;
    private const int ReadyToTurnIn = 3;
    private const long Thousand = 1_000;
    private const long Million = 1_000_000;
    private const long Billion = 1_000_000_000;

    public readonly record struct Row(int QuestId, int State, int Zone, int Level);

    public static int Compare(Row a, Row b)
    {
        int byZone = ZoneRank(a.Zone).CompareTo(ZoneRank(b.Zone));
        if (byZone != 0) return byZone;
        int byReady = (b.State == ReadyToTurnIn).CompareTo(a.State == ReadyToTurnIn);
        if (byReady != 0) return byReady;
        int byLevel = a.Level.CompareTo(b.Level);
        return byLevel != 0 ? byLevel : a.QuestId.CompareTo(b.QuestId);
    }

    private static long ZoneRank(int zone) => zone == AnywhereZone ? long.MaxValue : zone;

    public static bool Matches(string questName, string search) =>
        search.Length == 0 || questName.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase);

    public static string StatusLine(int level, string status) =>
        level > 0 ? $"Lv. {level}  ·  {status}" : status;

    public static string GroupTitle(string zoneName, int count) => $"{zoneName}  ({count})";

    public static float Fraction(int current, int target) =>
        target > 0 ? Math.Clamp(current / (float)target, 0f, 1f) : 0f;

    public static string CompactCount(long count) => count switch
    {
        >= Billion => Scaled(count, Billion, "B"),
        >= Million => Scaled(count, Million, "M"),
        >= Thousand => Scaled(count, Thousand, "K"),
        _ => count.ToString("n0", CultureInfo.InvariantCulture),
    };

    private static string Scaled(long count, long unit, string suffix)
    {
        double value = Math.Floor(count * 10.0 / unit) / 10.0;
        return value.ToString("0.#", CultureInfo.InvariantCulture) + suffix;
    }
}
