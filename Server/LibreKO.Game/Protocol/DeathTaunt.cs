using LibreKO.Common.Domain.Entities.GameData;
using LibreKO.Common.Enums;

namespace LibreKO.Game.Protocol;

public static class DeathTaunt
{
    public const int MarkerSlot = 1;
    public const int Marker = 1;

    public static bool IsTaunt(MagicData magic, MagicType3Data? type3) =>
        (SkillMoral)magic.Moral == SkillMoral.Enemy
        && magic.PrimaryType == MagicSkillType.OverTime
        && magic.SecondaryType == MagicSkillType.None
        && type3 is { DirectType: 0, FirstDamage: 0, TimeDamage: 0 };
}
