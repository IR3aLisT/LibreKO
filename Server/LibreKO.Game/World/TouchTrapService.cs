using LibreKO.Common.Domain.Entities.GameData;
using LibreKO.Game.Protocol;

namespace LibreKO.Game.World;

public interface ITouchTrapService
{
    Task<bool> TryTouchAsync(UserSession session, NpcInstance npc);
    Task HurtAsync(UserSession session, NpcInstance source, int damage);
}

public sealed class TouchTrapService(
    ICombatNotificationService combatNotificationService,
    INpcAiDeathService npcAiDeathService,
    ILoyaltyService loyaltyService) : ITouchTrapService
{
    public const int SawBladeNpcId = 32153;
    public const int SawBladeDamageInKrowaz = 2000;
    public const int SawBladeDamageElsewhere = 3000;
    public const int RollingStoneDamage = 1400;
    public const int WoodObjectDamage = 2800;
    public const int TrapDeathLoyalty = -100;

    public static int TouchDamage(NpcInstance npc, short zoneId)
    {
        if (npc.NpcType == NpcData.TypeObjectWood) return WoodObjectDamage;
        if (npc.NpcId == SawBladeNpcId) return zoneId == KrowazService.Zone ? SawBladeDamageInKrowaz : SawBladeDamageElsewhere;
        return npc.NpcType == NpcData.TypeRollingStone ? RollingStoneDamage : 0;
    }

    public async Task<bool> TryTouchAsync(UserSession session, NpcInstance npc)
    {
        int damage = TouchDamage(npc, session.ZoneId);
        if (damage <= 0 || session.Hp <= 0)
            return false;
        await HurtAsync(session, npc, damage);
        return true;
    }

    public async Task HurtAsync(UserSession session, NpcInstance source, int damage)
    {
        session.Hp -= (short)Math.Min(GmMode.Taken(session, damage), session.Hp);
        await combatNotificationService.SendHpChangeAsync(session, source.UniqueId);
        if (session.Hp > 0)
            return;

        await npcAiDeathService.HandlePlayerKilledByNpcAsync(session, source);
        if (session.ZoneId == KrowazService.Zone && (source.NpcId == SawBladeNpcId || source.NpcType == NpcData.TypeRollingStone))
            await loyaltyService.ChangeAsync(session, TrapDeathLoyalty);
    }
}
