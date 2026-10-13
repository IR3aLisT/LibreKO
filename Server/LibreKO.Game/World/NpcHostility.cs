using LibreKO.Common.Domain.Entities.GameData;
using LibreKO.Common.Enums;

namespace LibreKO.Game.World;

public static class NpcHostility
{
    public static bool IsAttackableBy(NpcInstance npc, UserSession player)
    {
        if (npc.NpcType == NpcData.TypeBifrostMonument)
            return npc.IsMonumentAttackable && npc.IsAlive;

        return npc.IsAttackable || IsEnemyNationNpc(npc, player);
    }

    private static bool IsEnemyNationNpc(NpcInstance npc, UserSession player)
        => npc.IsNationOwned
        && (byte)npc.Nation != (byte)player.Nation
        && !ZoneRules.Allows(npc.ZoneId, ZoneFlags.FriendlyNpcs);
}
