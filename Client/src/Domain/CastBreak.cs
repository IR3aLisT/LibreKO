namespace LibreKO.Domain;

public readonly record struct CastBreakSkill(
    bool Ranged, int FlyingFxId, int Type1, int Type2, int Attribute, int EndDamage, int TimeDamage)
{
    public static CastBreakSkill Of(SkillData.Skill s) =>
        new(s.IsRanged, s.FlyingFxId, s.Type1, s.Type2, s.Attribute, s.EndDamage, s.TimeDamage);
}

public static class CastBreak
{
    public const int LightningAttribute = 3;

    public static bool InFlightFailBreaks(CastBreakSkill s, short[] data)
    {
        if (data.Length <= MagicFail.ResultSlot || s.FlyingFxId <= 0) return false;
        short code = data[MagicFail.ResultSlot];
        if (code is not (MagicFail.ProjectileKilled or MagicFail.ProjectileKilledByType)) return false;
        if (s.Ranged) return code != MagicFail.ProjectileKilledByType;
        return IsLightningArea(s);
    }

    public static bool LightningHurts(CastBreakSkill s) =>
        IsLightningArea(s) && (s.EndDamage < 0 || (s.EndDamage == 0 && s.TimeDamage <= 0));

    private static bool IsLightningArea(CastBreakSkill s) =>
        (s.Type1 == MagicType.DotHeal || s.Type2 == MagicType.DotHeal) && s.Attribute == LightningAttribute;
}
