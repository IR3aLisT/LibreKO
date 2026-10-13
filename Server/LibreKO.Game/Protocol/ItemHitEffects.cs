using LibreKO.Common.Domain.Services;
using LibreKO.Game.World;

namespace LibreKO.Game.Protocol;

internal static class ItemHitEffects
{
    public static async Task ApplyAsync(
        UserSession attacker, UserSession? target, ItemHit items, int drainBase,
        ICombatLifecycleService lifecycle, IGameDataService gameData)
    {
        if (drainBase <= 0 || attacker.Hp <= 0)
            return;

        var healed = ItemCombatBonus.HpDrained(items, drainBase);
        if (healed > 0 && attacker.Hp < attacker.MaxHp)
        {
            attacker.Hp = (short)Math.Min(attacker.MaxHp, attacker.Hp + healed);
            await lifecycle.SendHpChangeAsync(attacker);
        }

        var drained = ItemCombatBonus.ManaDrained(items, drainBase);
        if (drained > 0 && attacker.Mp < attacker.MaxMp)
        {
            attacker.Mp = (short)Math.Min(attacker.MaxMp, attacker.Mp + drained);
            await lifecycle.SendMspChangeAsync(attacker);
        }

        if (target == null)
            return;

        var burned = ItemCombatBonus.ManaBurned(items, drainBase);
        if (burned > 0 && target.Mp > 0)
        {
            target.Mp = (short)Math.Max(0, target.Mp - burned);
            await lifecycle.SendMspChangeAsync(target);
        }

        var reflected = GmMode.Taken(attacker, ItemCombatBonus.Reflected(ItemCombatBonus.MirrorOf(target, gameData), drainBase));
        if (reflected <= 0)
            return;

        attacker.Hp = (short)Math.Max(0, attacker.Hp - reflected);
        attacker.MarkCombat();
        await lifecycle.SendHpChangeAsync(attacker, target.CharacterId);
        if (attacker.Hp <= 0)
            await lifecycle.HandlePlayerDeathAsync(attacker, target);
    }
}
