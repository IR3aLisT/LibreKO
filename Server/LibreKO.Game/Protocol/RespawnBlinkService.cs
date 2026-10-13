using LibreKO.Common.Domain.Entities.GameData;
using LibreKO.Common.Enums;
using LibreKO.Game.Protocol.Writers;
using LibreKO.Game.World;

namespace LibreKO.Game.Protocol;

public interface IRespawnBlinkService
{
    Task StartAsync(UserSession session);
    Task ExpireAsync(UserSession session, long nowTicks);
}

public sealed class RespawnBlinkService(SessionManager sessionManager) : IRespawnBlinkService
{
    public const int BlinkSeconds = 15;
    public const int SkillItemFirstId = 300000;

    public static bool Allowed(UserSession session) =>
        !session.IsGM
        && !session.IsInArena
        && !BattleZoneManager.IsFreeForAllZone(session.ZoneId)
        && !BattleZoneManager.AllowsNationCombat(session.ZoneId);

    public static bool AllowsSkill(MagicData magic) =>
        magic.PrimaryType == MagicSkillType.Buff || magic.Id >= SkillItemFirstId;

    public async Task StartAsync(UserSession session)
    {
        if (session.Hp <= 0 || !Allowed(session))
            return;

        session.BlinkUntilTicks = DateTime.UtcNow.AddSeconds(BlinkSeconds).Ticks;
        sessionManager.Regions.DropAggroOn(session.CharacterId);
        await Announce(session, PlayerAppearanceState.Blinking);
    }

    public async Task ExpireAsync(UserSession session, long nowTicks)
    {
        if (!session.IsBlinking || nowTicks < session.BlinkUntilTicks)
            return;

        session.BlinkUntilTicks = 0;
        await Announce(session, session.ZoneId == ChaosRules.Zone ? PlayerAppearanceState.ChaosNormal : session.SizeState);
    }

    private Task Announce(UserSession session, PlayerAppearanceState state) =>
        sessionManager.Regions.SendToRegion(
            session,
            MovementPacketWriter.StateChange(session.CharacterId, (byte)StateChangeType.Abnormal, (byte)state),
            excludeSender: false);
}
