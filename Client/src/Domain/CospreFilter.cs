namespace LibreKO.Domain;

[System.Flags]
public enum CospreKinds
{
    None = 0,
    Wings = 1,
    Fairies = 2,
    Talismans = 4,
    Emblems = 8,
    PathosGlow = 16,
}

public static class CospreFilter
{
    private static readonly (CospreKinds Kind, int Slot)[] Slots =
    {
        (CospreKinds.Wings, InventoryConstants.VisCosWing),
        (CospreKinds.Fairies, InventoryConstants.VisCosFairy),
        (CospreKinds.Talismans, InventoryConstants.VisCosTalisman),
        (CospreKinds.Emblems, InventoryConstants.VisCosEmblem),
        (CospreKinds.PathosGlow, InventoryConstants.VisCosGloveLeft),
        (CospreKinds.PathosGlow, InventoryConstants.VisCosGloveRight),
    };

    public static int[]? Hide(int[]? gear, CospreKinds hidden)
    {
        if (gear == null || hidden == CospreKinds.None) return gear;
        int[]? shown = null;
        foreach (var (kind, slot) in Slots)
        {
            if ((hidden & kind) == 0 || slot >= gear.Length || gear[slot] == 0) continue;
            shown ??= (int[])gear.Clone();
            shown[slot] = 0;
        }
        return shown ?? gear;
    }
}
