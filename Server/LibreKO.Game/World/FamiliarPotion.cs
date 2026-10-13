using LibreKO.Common.Domain.Entities.GameData;
using LibreKO.Common.Enums;

namespace LibreKO.Game.World;

public static class FamiliarPotion
{
    public const byte SelfMoral = 1;
    public const byte HpDirect = 1;

    public static int Heal(ItemData? item, MagicData? magic, MagicType3Data? overTime) =>
        item != null && magic != null && overTime != null
        && item.Effect1 == magic.Id && magic.ConsumedItem == item.Num
        && magic.PrimaryType == MagicSkillType.OverTime && magic.Moral == SelfMoral
        && overTime.DirectType == HpDirect && overTime.FirstDamage > 0
            ? overTime.FirstDamage
            : 0;
}
