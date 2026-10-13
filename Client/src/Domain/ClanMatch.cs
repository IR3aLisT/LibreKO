using System.Collections.Generic;
using LibreKO.Network;

namespace LibreKO.Domain;

public enum MatchBanner
{
    BlueWins = 2,
    RedWins = 3,
    Draw = 4,
}

public static class ClanMatch
{
    public const int TeamSlots = 2;
    public const int MembersPerTeam = 8;
    public const int RedTeam = 0;
    public const int BlueTeam = 1;

    private static readonly int[] PlateZones = { 96, 97, 98, 99 };
    private static readonly int[] ColourZones = { 77, 78, 96, 97, 98, 99 };

    public static bool ShowsPlate(int zone) => System.Array.IndexOf(PlateZones, zone) >= 0;

    public static bool ColoursNames(int zone, int pairZone) =>
        System.Array.IndexOf(ColourZones, zone) >= 0 && System.Array.IndexOf(ColourZones, pairZone) >= 0;

    public static MatchBanner BannerFor(int winnerClan, ClanMatchPair pair) =>
        winnerClan == pair.ClanA ? MatchBanner.RedWins : winnerClan == pair.ClanB ? MatchBanner.BlueWins : MatchBanner.Draw;

    public static int TeamOf(int clan, ClanMatchPair pair) => clan == pair.ClanA ? RedTeam : BlueTeam;

    public static int Percent(int current, int max) => max > 0 ? 100 * current / max : 0;

    public static List<ClanMatchFighter>[] Split(IReadOnlyList<ClanMatchFighter> roster)
    {
        var teams = new[] { new List<ClanMatchFighter>(), new List<ClanMatchFighter>() };
        var clans = new List<int>(TeamSlots);
        foreach (var fighter in roster)
        {
            int slot = clans.IndexOf(fighter.Clan);
            if (slot < 0)
            {
                if (clans.Count >= TeamSlots) continue;
                clans.Add(fighter.Clan);
                slot = clans.Count - 1;
            }
            if (teams[slot].Count < MembersPerTeam) teams[slot].Add(fighter);
        }
        return teams;
    }
}
