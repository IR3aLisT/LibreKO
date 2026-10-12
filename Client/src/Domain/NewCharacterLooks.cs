using System.Collections.Generic;
using System.Text.Json;

namespace LibreKO.Domain;

public readonly record struct NewCharacterLook(int Right, int Left, int Top, int Bottom, int Gloves, int Boots, int Helmet);

public static class NewCharacterLooks
{
    public const string ResourcePath = "res://assets/characters/new_characters.json";

    public static Dictionary<(int Race, int Class), NewCharacterLook> Parse(string json)
    {
        var looks = new Dictionary<(int, int), NewCharacterLook>();
        using var doc = JsonDocument.Parse(json);
        foreach (var row in doc.RootElement.EnumerateObject())
        {
            var parts = row.Name.Split('_');
            if (parts.Length != 2 || !int.TryParse(parts[0], out int race) || !int.TryParse(parts[1], out int cls)) continue;
            var v = row.Value;
            looks[(race, cls)] = new NewCharacterLook(
                Item(v, "right"), Item(v, "left"), Item(v, "top"), Item(v, "bottom"),
                Item(v, "gloves"), Item(v, "boots"), Item(v, "helmet"));
        }
        return looks;
    }

    private static int Item(JsonElement row, string name) =>
        row.TryGetProperty(name, out var value) && value.TryGetInt32(out int id) ? id : 0;

    public static int[] Gear(NewCharacterLook look, bool helmet)
    {
        var gear = new int[InventoryConstants.VisualSlotCount];
        gear[InventoryConstants.VisRightHand] = look.Right;
        gear[InventoryConstants.VisLeftHand] = look.Left;
        gear[InventoryConstants.VisBreast] = look.Top;
        gear[InventoryConstants.VisLeg] = look.Bottom;
        gear[InventoryConstants.VisGlove] = look.Gloves;
        gear[InventoryConstants.VisFoot] = look.Boots;
        gear[InventoryConstants.VisHead] = helmet ? look.Helmet : 0;
        return gear;
    }
}
