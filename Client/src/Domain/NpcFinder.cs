using System.Collections.Generic;
using System.Text.Json;

namespace LibreKO.Domain;

public readonly record struct NpcSpot(int Map, int Kinds, float X, float Z, int NpcId, int Nation, string Name)
{
    private const int FirstShift = 10000;
    private const int SecondShift = 100;

    public bool Is(int kind) =>
        kind > 0 && (Kinds / FirstShift == kind || Kinds % FirstShift / SecondShift == kind || Kinds % SecondShift == kind);
}

public static class NpcFinder
{
    public const string ResourcePath = "res://assets/npcs/npc_finder.json";
    public const int NoKind = 0;

    public static readonly string[] KindCaptions =
    {
        "General Shop", "Potion shop", "Inn owner", "Repair", "Power up store", "Weapon Shop",
        "Armor Shop", "Accessory Shop", "Combine", "Event", "Pet Shop", "Royal enchanter",
    };

    public static int KindCount => KindCaptions.Length;

    private const int ZoneKeyScale = 10;
    private const int OtherMap = 20;
    private const int BothNations = 0;
    private const int RowFields = 7;

    private static readonly Dictionary<int, int> MapByKey = new()
    {
        [10] = 1, [20] = 2, [110] = 8, [120] = 9, [180] = 15, [210] = 3, [280] = 16, [300] = 4,
        [610] = 5, [620] = 6, [630] = 7, [640] = 12, [660] = 14, [710] = 11, [720] = 10, [730] = 13, [860] = 17,
    };

    public static List<NpcSpot> Parse(string json)
    {
        var spots = new List<NpcSpot>();
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("spots", out var rows)) return spots;
        foreach (var row in rows.EnumerateArray())
        {
            if (row.GetArrayLength() < RowFields) continue;
            spots.Add(new NpcSpot(row[0].GetInt32(), row[1].GetInt32(), row[2].GetSingle(), row[3].GetSingle(),
                row[4].GetInt32(), row[5].GetInt32(), row[6].GetString() ?? ""));
        }
        return spots;
    }

    private const int MoradonKey = 210;
    private const int LastMoradonCopyKey = 250;
    private const int Zone84Key = 840;
    private const int FirstZone84CopyKey = 960;
    private const int LastZone84CopyKey = 990;

    private static readonly Dictionary<int, int> CopyKeys = new()
    {
        [50] = 10, [60] = 10, [70] = 20, [80] = 20, [130] = 110, [140] = 110, [150] = 120, [160] = 120,
    };

    public static int ZoneKey(int zone)
    {
        int key = zone * ZoneKeyScale;
        if (key is >= MoradonKey and <= LastMoradonCopyKey) return MoradonKey;
        if (key is >= FirstZone84CopyKey and <= LastZone84CopyKey) return Zone84Key;
        return CopyKeys.TryGetValue(key, out int group) ? group : key;
    }

    public static int MapOf(int zone) => MapByKey.TryGetValue(ZoneKey(zone), out int map) ? map : OtherMap;

    public static bool Shown(NpcSpot spot, int map, int nation) =>
        spot.Map == map && (spot.Nation == BothNations || spot.Nation == nation);

    public static IEnumerable<NpcSpot> Spots(IReadOnlyList<NpcSpot> all, int zone, int nation, int kind)
    {
        int map = MapOf(zone);
        foreach (var spot in all)
            if (Shown(spot, map, nation) && spot.Is(kind))
                yield return spot;
    }

    public static bool[] KindsPresent(IReadOnlyList<NpcSpot> all, int zone, int nation)
    {
        var present = new bool[KindCount];
        int map = MapOf(zone);
        foreach (var spot in all)
        {
            if (!Shown(spot, map, nation)) continue;
            for (int kind = 1; kind <= KindCount; kind++)
                if (spot.Is(kind)) present[kind - 1] = true;
        }
        return present;
    }
}
