using System.Collections.Generic;
using Godot;

namespace LibreKO.Domain;

public static class SkillMsgOut
{
    private const string Path = "res://assets/skills/msgout.json";
    private static readonly Dictionary<int, string> _cast = new();
    private static readonly Dictionary<int, string> _effect = new();
    private static bool _loaded;

    public static string? Cast(int skillId) => Lookup(_cast, skillId);

    public static string? Effect(int skillId) => Lookup(_effect, skillId);

    private static string? Lookup(Dictionary<int, string> table, int skillId)
    {
        EnsureLoaded();
        return table.TryGetValue(skillId, out var text) ? text : null;
    }

    private static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        using var f = Godot.FileAccess.Open(Path, Godot.FileAccess.ModeFlags.Read);
        if (f == null) return;
        var parsed = Json.ParseString(f.GetAsText());
        if (parsed.VariantType != Variant.Type.Dictionary) return;
        var root = parsed.AsGodotDictionary();
        LoadInto(root, "cast", _cast);
        LoadInto(root, "effect", _effect);
    }

    private static void LoadInto(Godot.Collections.Dictionary root, string key, Dictionary<int, string> into)
    {
        if (!root.TryGetValue(key, out var v) || v.VariantType != Variant.Type.Dictionary) return;
        var map = v.AsGodotDictionary();
        foreach (var k in map.Keys)
            if (int.TryParse(k.AsString(), out int id))
                into[id] = map[k].AsString();
    }
}
