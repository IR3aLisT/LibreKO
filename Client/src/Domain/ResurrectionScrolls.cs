using System.Collections.Generic;

namespace LibreKO.Domain;

public readonly record struct ScrollSkill(int SkillId, string Name, int Moral, int SpecialType, int ExpRecover);

public static class ResurrectionScrolls
{
    public const int FirstSkill = 480000;
    public const int LastSkill = 489999;
    public const int ExcludedSkill = 480004;
    public const int SelfResurrect = 4;
    public const int LifeCrystal = 6;
    public const int LifeCrystalZone = 86;
    public const int MinLevelExclusive = 5;
    public const int MaxRows = 3;

    public static bool Usable(ScrollSkill s, int zone) =>
        s.SkillId is >= FirstSkill and <= LastSkill
        && s.Moral == SkillTarget.Self
        && s.SpecialType == (zone == LifeCrystalZone ? LifeCrystal : SelfResurrect);

    public static List<ScrollSkill> Offer(IEnumerable<ScrollSkill> carried, int zone, int level)
    {
        var offer = new List<ScrollSkill>(MaxRows);
        if (level <= MinLevelExclusive) return offer;
        var seen = new HashSet<int>();
        foreach (var s in carried)
        {
            if (offer.Count == MaxRows) break;
            if (s.SkillId == ExcludedSkill || !Usable(s, zone) || !seen.Add(s.SkillId)) continue;
            offer.Add(s);
        }
        return offer;
    }

    public static int BestIndex(IReadOnlyList<ScrollSkill> offer)
    {
        int best = -1, bestRecover = int.MinValue;
        for (int i = 0; i < offer.Count; i++)
        {
            if (offer[i].ExpRecover <= bestRecover) continue;
            bestRecover = offer[i].ExpRecover;
            best = i;
        }
        return best;
    }
}
