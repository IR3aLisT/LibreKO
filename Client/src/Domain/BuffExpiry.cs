namespace LibreKO.Domain;

public static class BuffExpiry
{
    public const int DamageOverTimeCured = 200;
    public const int HealOverTimeEnded = 100;
    public const int StealthEnded = 91;
    public const int SightEnded = 92;
    public const int SummonEnded = 93;

    public static bool Removes(int code, int buffType, int type1, int timeDamage, int stateChange = 0) => code switch
    {
        DamageOverTimeCured => type1 == MagicType.DotHeal && timeDamage < 0,
        HealOverTimeEnded => type1 == MagicType.DotHeal && timeDamage > 0,
        StealthEnded or SightEnded or SummonEnded when type1 == MagicType.Stealth
            => StealthKind.ExpiryCode(stateChange) == code,
        _ => code != 0 && buffType == code,
    };

    public const double BlinkSeconds = 10.0;
    public const float BlinkDimAlpha = 0.3f;
    private const double BlinkHalf = 0.5;

    public static bool BlinkDimmed(double secondsLeft) =>
        secondsLeft <= BlinkSeconds && secondsLeft - System.Math.Floor(secondsLeft) >= BlinkHalf;
}
