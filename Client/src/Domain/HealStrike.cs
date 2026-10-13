namespace LibreKO.Domain;

public static class HealStrike
{
    private const int HealthDirect = 1;

    public static bool StrikesWith(int type1, int directType, int firstDamage, int timeDamage) =>
        type1 == MagicType.DotHeal && (directType == HealthDirect || (firstDamage > 0 && timeDamage > 0));
}
