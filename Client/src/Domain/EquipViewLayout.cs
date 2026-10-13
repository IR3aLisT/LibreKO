namespace LibreKO.Domain;

public static class EquipViewLayout
{
    public const int Columns = 3;

    public static readonly int[] CostumeCells =
    {
        InventoryConstants.CosTattoo, InventoryConstants.CosHelmet, InventoryConstants.CosFairy,
        InventoryConstants.CosGloveRight, InventoryConstants.CosPauldron, InventoryConstants.CosGloveLeft,
        InventoryConstants.CosTalisman, InventoryConstants.CosWing, InventoryConstants.CosEmblem,
    };

    public static readonly int[] CostumePositions = { 8, 1, 7, 2, 4, 3, 9, 0, 5 };

    public static int SlotOfCostumePosition(int position)
    {
        int index = System.Array.IndexOf(CostumePositions, position);
        return index >= 0 ? CostumeCells[index] : -1;
    }

    public static string LevelLine(int level, int rebirth) => rebirth > 0 ? $"{level} / {rebirth}" : level.ToString();

    public static string StatLine(int stat, int bonus) => bonus > 0 ? $"{stat}(+{bonus})" : stat.ToString();
}
