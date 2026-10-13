using System;
using System.Collections.Generic;
using LibreKO.Network;

namespace LibreKO.Domain;

public static class PartyAutoEntry
{
    public const byte PartyPost = 3;
    public const int MaxMembers = 8;
    public const int ScannedRows = 12;
    public const double RetrySeconds = 5;
    public const int MaxLevel = 83;
    public const int NoneText = 15411;
    public const int SearchingText = 15412;
    public const int AppliedText = 3424;
    public const int AnyClass = 0;
    private const int LevelSpread = 8;
    private const double LevelRatio = 1.5;

    public static bool LevelFits(int postLevel, int myLevel)
    {
        int low = Math.Min(postLevel - LevelSpread, (int)(postLevel / LevelRatio));
        if (low < 0) low = 1;
        int high = Math.Min(MaxLevel, Math.Max(postLevel + LevelSpread, (int)(postLevel * LevelRatio)));
        return myLevel >= low && myLevel <= high;
    }

    public static bool ClassFits(int wanted, int classCode) =>
        wanted == AnyClass || wanted == CharacterClassCatalog.Family(classCode);

    public static int Pick(IReadOnlyList<PartyBbsEntry> entries, int zone, int classCode, int level)
    {
        for (int i = 0; i < entries.Count && i < ScannedRows; i++)
        {
            var e = entries[i];
            if (e.Type == PartyPost && e.ZoneId == zone && e.MemberCount < MaxMembers
                && ClassFits(e.ClassOrWanted, classCode) && LevelFits(e.Level, level))
                return i;
        }
        return -1;
    }
}
