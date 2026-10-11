using System.Collections.Generic;

namespace LibreKO.Domain;

public static class WarpRows
{
    public static string? BlockReason(int level, long gold, int minLevel, int maxLevel, long fee)
    {
        if (minLevel > 0 && level < minLevel) return $"Requires level {minLevel}.";
        if (maxLevel > 0 && level > maxLevel) return $"Level {maxLevel} and below only.";
        if (fee > gold) return $"Need {fee:n0} gold.";
        return null;
    }

    public static string FeeText(long fee) => fee > 0 ? $"{fee:n0} gold" : "Free";

    public static string LevelText(int minLevel, int maxLevel) => (minLevel > 0, maxLevel > 0) switch
    {
        (true, true) => $"lv{minLevel}  ~  lv{maxLevel}",
        (true, false) => $"lv{minLevel}+",
        (false, true) => $"up to lv{maxLevel}",
        _ => "",
    };

    public static int FirstUsable(IReadOnlyList<string?> reasons)
    {
        for (int i = 0; i < reasons.Count; i++)
            if (reasons[i] == null) return i;
        return 0;
    }
}
