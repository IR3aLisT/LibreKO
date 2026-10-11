using LibreKO.Common.Domain.Entities.GameData;

namespace LibreKO.Game.Protocol;

public static class UpgradeAnnouncements
{
    public const int WeaponArmourGrade = 8;
    public const int ReverseGrade = 11;
    public const int AccessoryGrade = 2;
    public const short ReverseItemClass = 4;
    private const byte FirstAccessoryKind = 91;
    private const byte LastAccessoryKind = 94;

    public static bool Announced(ItemData item, int grade) =>
        item.Kind is >= FirstAccessoryKind and <= LastAccessoryKind ? grade >= AccessoryGrade
        : item.ItemClass == ReverseItemClass ? grade >= ReverseGrade
        : grade >= WeaponArmourGrade;
}
