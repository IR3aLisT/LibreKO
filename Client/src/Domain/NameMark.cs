using System.Collections.Generic;
using LibreKO.Network;

namespace LibreKO.Domain;

public enum NameMarkKind
{
    None,
    Underline,
    Box,
}

public static class NameMark
{
    public const int PromotedClanFlag = 2;

    private static readonly HashSet<int> UnmarkedZones = new() { 29, 45, 85, 89 };
    private static readonly HashSet<int> OwnNationOnlyZones = new() { 31, 46 };

    public static bool HidesClan(int zone) => UnmarkedZones.Contains(zone);

    public static NameMarkKind For(int fame, int clanFlag, int zone, bool sameNation)
    {
        if (UnmarkedZones.Contains(zone)) return NameMarkKind.None;
        if (OwnNationOnlyZones.Contains(zone) && !sameNation) return NameMarkKind.None;
        if (fame == ClanRanks.CommandCaptain) return NameMarkKind.Box;
        return fame == ClanRanks.Chief && clanFlag > PromotedClanFlag ? NameMarkKind.Underline : NameMarkKind.None;
    }
}
