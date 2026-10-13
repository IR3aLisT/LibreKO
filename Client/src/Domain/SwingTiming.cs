namespace LibreKO.Domain;

public static class SwingTiming
{
    public const float BareHandedSeconds = 1f;
    private const float PercentScale = 100f;

    public static float IntervalSeconds(int weaponDelay, int intervalPercent, float attackSpeed)
    {
        float seconds = BareHandedSeconds;
        if (weaponDelay > 0)
        {
            float percent = intervalPercent > 0 ? intervalPercent : PercentScale;
            seconds = weaponDelay / PercentScale * (percent / PercentScale);
        }
        return attackSpeed > 0f ? seconds / attackSpeed : seconds;
    }
}
