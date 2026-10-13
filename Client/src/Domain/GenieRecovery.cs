namespace LibreKO.Domain;

public static class GenieRecovery
{
    public const int HpDirect = 1;
    public const int MpDirect = 2;
    public const int FeedBelowSatisfaction = 500;
    public const double FeedRetrySeconds = 5;
    public const double PetPotionRetrySeconds = 2;

    public static bool RestoresHp(bool friendly, int type1, int directType, int firstDamage, int timeDamage) =>
        friendly && type1 == MagicType.DotHeal && directType == HpDirect && (firstDamage > 0 || timeDamage > 0);

    public static bool RestoresMp(bool friendly, int type1, int directType, int firstDamage) =>
        friendly && type1 == MagicType.DotHeal && directType == MpDirect && firstDamage > 0;

    public static bool Below(int value, int max, int percent) => max > 0 && value * 100L <= (long)max * percent;

    public static bool WantsFeed(bool enabled, bool summoned, int satisfaction) =>
        enabled && summoned && satisfaction < FeedBelowSatisfaction;
}
