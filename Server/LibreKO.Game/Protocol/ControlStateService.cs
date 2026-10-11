using LibreKO.Common.Enums;
using LibreKO.Game.Protocol.Writers;
using LibreKO.Game.World;

namespace LibreKO.Game.Protocol;

public static class ControlFlags
{
    public const byte Stunned = 1;
    public const byte Frozen = 2;
    public const byte Blinded = 4;
    public const byte Slowed = 8;
    public const byte AttackSlowed = 16;
}

public interface IControlStateService
{
    Task SyncAsync(UserSession session);
    Task SyncAsync(NpcInstance npc);
}

public sealed class ControlStateService(SessionManager sessionManager) : IControlStateService
{
    public const byte FullPercent = 100;

    public async Task SyncAsync(UserSession session)
    {
        var state = Of(session, DateTime.UtcNow.Ticks);
        var signature = Signature(state);
        if (signature == session.ControlSignature)
            return;

        session.ControlSignature = signature;
        await sessionManager.Regions.SendToRegion(
            session, TargetStatePacketWriter.Control(session.CharacterId, state), excludeSender: false);
    }

    public async Task SyncAsync(NpcInstance npc)
    {
        var state = Of(npc, DateTime.UtcNow.Ticks);
        var signature = Signature(state);
        if (signature == npc.ControlSignature)
            return;

        npc.ControlSignature = signature;
        await sessionManager.Regions.BroadcastFromNpc(npc, TargetStatePacketWriter.Control(npc.UniqueId, state));
    }

    public static ControlState Of(UserSession session, long nowTicks)
    {
        byte flags = 0;
        int move = FullPercent, attack = FullPercent;
        var effects = new List<ControlEffect>();
        if (session.Hp > 0)
        {
            foreach (var buff in session.ActiveBuffs.Values)
            {
                if (buff.ExpireTicks <= nowTicks) continue;
                var flag = FlagOf(buff.BuffType, buff.BonusSpeed, buff.BonusAttackSpeed);
                if (flag == 0) continue;
                flags |= flag;
                if (flag == ControlFlags.Slowed) move = Math.Min(move, buff.BonusSpeed);
                if (flag == ControlFlags.AttackSlowed) attack = Math.Min(attack, buff.BonusAttackSpeed);
                effects.Add(new ControlEffect(buff.MagicId, (byte)buff.BuffType, Milliseconds(buff.ExpireTicks - nowTicks)));
            }
        }
        return new ControlState(flags, (byte)Math.Clamp(move, 0, FullPercent), (short)attack, effects);
    }

    public static ControlState Of(NpcInstance npc, long nowTicks)
    {
        byte flags = 0;
        int move = FullPercent;
        var effects = new List<ControlEffect>();
        if (npc.Hp > 0)
        {
            foreach (var debuff in npc.Debuffs.Active(nowTicks))
            {
                var flag = FlagOf(debuff.Type, debuff.SpeedPercent, FullPercent);
                if (flag == 0) continue;
                flags |= flag;
                if (flag == ControlFlags.Slowed) move = Math.Min(move, debuff.SpeedPercent);
                effects.Add(new ControlEffect(debuff.SkillId, (byte)debuff.Type, Milliseconds(debuff.ExpireTicks - nowTicks)));
            }
        }
        return new ControlState(flags, (byte)Math.Clamp(move, 0, FullPercent), FullPercent, effects);
    }

    public static byte FlagOf(BuffType type, int speedPercent, int attackSpeedPercent) => type switch
    {
        BuffType.Stun or BuffType.Sleep => ControlFlags.Stunned,
        BuffType.Freeze => ControlFlags.Frozen,
        BuffType.Blind or BuffType.DisableTargeting or BuffType.Unsight => ControlFlags.Blinded,
        BuffType.Speed or BuffType.Speed2 when speedPercent is > 0 and < FullPercent => ControlFlags.Slowed,
        BuffType.AttackSpeed when attackSpeedPercent is > 0 and < FullPercent => ControlFlags.AttackSlowed,
        _ => 0,
    };

    private static int Signature(ControlState state)
    {
        if (state.Flags == 0 && state.Effects.Count == 0)
            return 0;

        var hash = new HashCode();
        hash.Add(state.Flags);
        hash.Add(state.MovePercent);
        hash.Add(state.AttackPercent);
        foreach (var effect in state.Effects)
            hash.Add(effect.SkillId);
        var value = hash.ToHashCode();
        return value == 0 ? 1 : value;
    }

    private static int Milliseconds(long ticks) =>
        (int)Math.Clamp(ticks / TimeSpan.TicksPerMillisecond, 0, int.MaxValue);
}
