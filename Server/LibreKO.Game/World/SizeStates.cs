using LibreKO.Common.Enums;
using LibreKO.Game.Protocol.Writers;

namespace LibreKO.Game.World;

public static class SizeStates
{
    public const int BezoarSkill = 490034;
    public const int MaximizeScrollSkill = 490401;
    public const int RiceCakeSkill = 490035;
    public const int MinimizeScrollSkill = 490100;

    public static PlayerAppearanceState? Of(BuffType type, int skillId) => type switch
    {
        BuffType.ReduceTarget => PlayerAppearanceState.GiantTarget,
        BuffType.Size => skillId switch
        {
            BezoarSkill => PlayerAppearanceState.Giant,
            MaximizeScrollSkill => PlayerAppearanceState.GiantTarget,
            RiceCakeSkill or MinimizeScrollSkill => PlayerAppearanceState.Dwarf,
            _ => null,
        },
        _ => null,
    };

    public static PlayerAppearanceState Current(UserSession session)
    {
        foreach (var (skillId, buff) in session.ActiveBuffs)
            if (!buff.IsExpired && Of(buff.BuffType, skillId) is { } state)
                return state;
        return PlayerAppearanceState.Normal;
    }
}
