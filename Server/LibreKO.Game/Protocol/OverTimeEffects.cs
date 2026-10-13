using LibreKO.Common.Enums;
using LibreKO.Game.World;

namespace LibreKO.Game.Protocol;

internal static class OverTimeEffects
{
    public const byte TickSeconds = 2;
    private const int PercentScale = 100;

    public static byte PartyStatusOf(byte attribute) => (byte)((MagicAttribute)attribute switch
    {
        MagicAttribute.Disease => PartyStatusIcon.Disease,
        MagicAttribute.Poison => PartyStatusIcon.Poison,
        _ => PartyStatusIcon.OverTimeDamage,
    });

    public static int PercentOfHealth(int currentHp, int maxHp, int firstDamage) =>
        firstDamage < PercentScale
            ? firstDamage * currentHp / -PercentScale
            : maxHp * (firstDamage - PercentScale) / PercentScale;

    public static void Schedule(
        IDictionary<int, ActiveOverTimeEffect> activeEffects,
        int skillId,
        int casterId,
        int totalAmount,
        byte duration)
    {
        if (duration == 0 || totalAmount == 0)
        {
            activeEffects.Remove(skillId);
            return;
        }

        var tickCountFloat = duration / (float)TickSeconds;
        var tickLimit = Math.Max(1, (int)tickCountFloat);
        var tickAmount = (short)(totalAmount / tickCountFloat);
        if (tickAmount == 0)
        {
            activeEffects.Remove(skillId);
            return;
        }

        activeEffects[skillId] = new ActiveOverTimeEffect
        {
            MagicId = skillId,
            CasterId = casterId,
            TickAmount = tickAmount,
            TickIntervalSeconds = TickSeconds,
            TickCount = 0,
            TickLimit = (byte)Math.Min(byte.MaxValue, tickLimit),
            NextTickTicks = DateTime.UtcNow.AddSeconds(TickSeconds).Ticks
        };
    }
}
