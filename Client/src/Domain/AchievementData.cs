using System.Collections.Generic;
using Godot;

namespace LibreKO.Domain;

public enum AchievementTab
{
    Normal = 0,
    Quest = 1,
    War = 2,
    Adventure = 3,
    Challenge = 4,
}

public static class AchievementData
{
    public static readonly AchievementTab[] Tabs =
    {
        AchievementTab.Normal, AchievementTab.Quest, AchievementTab.War,
        AchievementTab.Adventure, AchievementTab.Challenge,
    };

    public readonly record struct Info(
        AchievementTab Tab, int Group, int Points, string Name, string Objective,
        int ItemId, int ItemCount, int TitleId);

    public readonly record struct Title(string Name, int AchievementId, string Bonus);

    private static readonly Dictionary<int, Info> Table = new();
    private static readonly Dictionary<int, Title> Titles = new();
    private static bool _loaded;

    public static string TabName(AchievementTab tab) => tab switch
    {
        AchievementTab.Normal => "Normal",
        AchievementTab.Quest => "Quest",
        AchievementTab.War => "War",
        AchievementTab.Adventure => "Adventure",
        AchievementTab.Challenge => "Challenge",
        _ => $"Tab {(int)tab}",
    };

    public const int AllGroups = -1;
    public const int GroupsPerTab = 10;
    private const int CategoryTextBase = 37000;
    private const int CategoryTextCount = 50;
    private const int CategoryTextFallback = 37050;

    public static AchievementTab TabOfGroup(int group) => (AchievementTab)(group / GroupsPerTab);

    public static List<int> CategoriesOf(AchievementTab tab)
    {
        EnsureLoaded();
        var groups = new SortedSet<int>();
        foreach (var info in Table.Values)
            if (info.Group >= 0 && TabOfGroup(info.Group) == tab) groups.Add(info.Group);
        return new List<int>(groups);
    }

    public static string CategoryName(int group)
    {
        int textId = group is >= 0 and < CategoryTextCount ? CategoryTextBase + group : CategoryTextFallback;
        return ItemData.Text(textId, $"Group {group}");
    }

    public static Info? Get(int achievementId)
    {
        EnsureLoaded();
        return Table.TryGetValue(achievementId, out var info) ? info : null;
    }

    public static string NameOf(int achievementId) =>
        Get(achievementId)?.Name ?? $"Achievement {achievementId}";

    public static int Count
    {
        get { EnsureLoaded(); return Table.Count; }
    }

    public static IEnumerable<int> Ids
    {
        get { EnsureLoaded(); return Table.Keys; }
    }

    private static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;

        using var file = Godot.FileAccess.Open(
            "res://assets/achievements/achievements.json", Godot.FileAccess.ModeFlags.Read);
        if (file == null)
        {
            GD.PushWarning("[achievements] achievements.json missing — the window will show ids only");
            return;
        }

        if (Json.ParseString(file.GetAsText()).Obj is not Godot.Collections.Dictionary root) return;

        foreach (var key in root.Keys)
        {
            if (!int.TryParse(key.AsString(), out int id)) continue;
            if (root[key].Obj is not Godot.Collections.Dictionary row) continue;

            Table[id] = new Info(
                (AchievementTab)Read(row, "tab"),
                Read(row, "group"),
                Read(row, "points"),
                row.ContainsKey("name") ? row["name"].AsString() : string.Empty,
                row.ContainsKey("objective") ? row["objective"].AsString() : string.Empty,
                Read(row, "item"),
                Read(row, "count"),
                Read(row, "title"));
        }

        LoadTitles();

        GD.Print($"[achievements] {Table.Count} definitions");
    }

    public static Title? TitleOf(int titleId)
    {
        EnsureLoaded();
        return Titles.TryGetValue(titleId, out var title) ? title : null;
    }

    private static void LoadTitles()
    {
        using var file = Godot.FileAccess.Open(
            "res://assets/achievements/titles.json", Godot.FileAccess.ModeFlags.Read);
        if (file == null) return;
        if (Json.ParseString(file.GetAsText()).Obj is not Godot.Collections.Dictionary root) return;

        foreach (var key in root.Keys)
        {
            if (!int.TryParse(key.AsString(), out int id)) continue;
            if (root[key].Obj is not Godot.Collections.Dictionary row) continue;

            Titles[id] = new Title(
                row.ContainsKey("name") ? row["name"].AsString() : string.Empty,
                Read(row, "achievement"),
                DescribeBonus(row));
        }
    }

    private static string DescribeBonus(Godot.Collections.Dictionary row)
    {
        if (!row.ContainsKey("bonus")) return string.Empty;
        if (row["bonus"].Obj is not Godot.Collections.Dictionary bonus) return string.Empty;

        var parts = new List<string>();
        foreach (var key in bonus.Keys)
            parts.Add(BonusLine(key.AsString(), (int)bonus[key]));
        return string.Join(", ", parts);
    }

    private const int BonusTextFirst = 37201;

    private static readonly (string Key, string Fallback)[] Bonuses =
    {
        ("str", "Strength: %d"), ("hp", "Health: %d"), ("dex", "Dexterity: %d"),
        ("int", "Intelligence: %d"), ("mp", "Magic Power: %d"), ("attack", "Attack: %d"),
        ("defence", "Defense: %d"), ("loyalty", "Contribution: %d (Restrictions apply)"),
        ("exp", "Base EXP for Monster Kills +%d%%"),
        ("ac_shortsword", "Dagger Defense: %d"), ("ac_jamadar", "Jamadar Defense: %d"),
        ("ac_sword", "Sword Defense: %d"), ("ac_blow", "Club Defense: %d"), ("ac_axe", "Axe Defense: %d"),
        ("ac_spear", "Spear Defense: %d"), ("ac_arrow", "Arrow Defense: %d"),
        ("fire", "Flame Bonus: %d"), ("ice", "Ice Bonus: %d"), ("light", "Electric Shock Bonus: %d"),
        ("r_fire", "Flame Resistence: %d"), ("r_ice", "Ice Resistence: %d"),
        ("r_light", "Electric Shock Resistence: %d"), ("r_magic", "Magic Resistence: %d"),
        ("r_curse", "Spell Resistence: %d"), ("r_poison", "Poison Resistence: %d"),
    };

    private static string BonusLine(string key, int value)
    {
        int index = System.Array.FindIndex(Bonuses, bonus => bonus.Key == key);
        if (index < 0) return $"{key}: {value}";
        return ItemData.Text(BonusTextFirst + index, Bonuses[index].Fallback)
            .Replace("%d", value.ToString())
            .Replace("%%", "%");
    }

    private static int Read(Godot.Collections.Dictionary row, string key) =>
        row.ContainsKey(key) ? (int)row[key] : 0;
}
