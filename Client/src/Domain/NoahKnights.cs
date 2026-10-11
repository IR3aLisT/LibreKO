using Godot;
using LibreKO.Network;

namespace LibreKO.Domain;

public static class NoahKnights
{
    public const int MaxLevel = 50;
    public const int GraduationLevel = MaxLevel + 1;
    public static readonly Color PlateColour = Color.Color8(238, 130, 238);

    public static bool IsMember(int level, int clanDuty) => level <= MaxLevel && clanDuty == ClanRanks.None;

    public static bool ShowsPlateLine(string clanName, int level, int clanDuty, bool hasStall) =>
        clanName.Length == 0 && !hasStall && IsMember(level, clanDuty);
}
