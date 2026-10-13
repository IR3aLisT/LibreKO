using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace LibreKO.Domain;

public readonly record struct BuiltInPlug(int Slot, int Joint, string Model, Vector3 Position, Quaternion Rotation,
                                          Vector3 Scale, string Anim);

public static class NpcBuiltInPlugs
{
    private const int VectorLength = 3;
    private const int QuaternionLength = 4;

    public static IReadOnlyList<BuiltInPlug> Parse(string json)
    {
        var plugs = new List<BuiltInPlug>();
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("plugs", out var list) || list.ValueKind != JsonValueKind.Array) return plugs;
        foreach (var e in list.EnumerateArray())
        {
            if (!e.TryGetProperty("slot", out var slot) || !e.TryGetProperty("joint", out var joint)
                || !e.TryGetProperty("model", out var model)
                || Floats(e, "pos", VectorLength) is not { } pos
                || Floats(e, "quat", QuaternionLength) is not { } quat
                || Floats(e, "scale", VectorLength) is not { } scale)
                continue;
            plugs.Add(new BuiltInPlug(slot.GetInt32(), joint.GetInt32(), model.GetString() ?? "",
                new Vector3(pos[0], pos[1], pos[2]), new Quaternion(quat[0], quat[1], quat[2], quat[3]),
                new Vector3(scale[0], scale[1], scale[2]),
                e.TryGetProperty("anim", out var anim) ? anim.GetString() ?? "" : ""));
        }
        return plugs;
    }

    private static float[]? Floats(JsonElement e, string name, int length)
    {
        if (!e.TryGetProperty(name, out var a) || a.ValueKind != JsonValueKind.Array || a.GetArrayLength() != length)
            return null;
        var values = new float[length];
        int i = 0;
        foreach (var v in a.EnumerateArray()) values[i++] = v.GetSingle();
        return values;
    }
}
