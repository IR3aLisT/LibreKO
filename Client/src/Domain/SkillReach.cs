using System.Collections.Generic;

namespace LibreKO.Domain;

public enum SkillReachVerdict
{
    InRange,
    TooFar,
    TooNear,
    OutOfReachSilent,
}

public readonly record struct SkillReachQuery
{
    public int SkillId { get; init; }
    public int Type1 { get; init; }
    public int Type2 { get; init; }
    public int Range { get; init; }
    public int SelfAnim2 { get; init; }
    public float Distance { get; init; }
    public float CasterRadius { get; init; }
    public float TargetRadius { get; init; }
    public bool TargetIsPlayer { get; init; }
    public int Zone { get; init; }
    public float MeleeWeaponReach { get; init; }
    public float RangedWeaponReach { get; init; }
    public int RangedAddRangePercent { get; init; }
    public float MeleeReachBonus { get; init; }
}

public static class SkillReach
{
    public const int KindNone = 0;
    public const int KindMelee = MagicType.Melee;
    public const int KindRanged = MagicType.Ranged;
    public const int KindMagic = MagicType.DotHeal;

    public const int SpotNone = 0;
    public const int SpotCrouch = 1;
    public const int SpotPull = 2;
    public const int SpotRush = 3;

    private const float RadiusGap = 1.0f;
    private const float FlatRadii = 2.0f;
    private const float ExtraUnit = 1.0f;
    private const float RushMinimum = 5.0f;
    private const float FlatMeleeReach = 5.0f;
    private const float RangedPlayerShortcut = 38.0f;
    private const float MagicPlayerOverRange = 15.0f;
    private const float PercentScale = 100f;

    private const int SiegeShellSelfAnim = 113;
    private const int StatScrollSkill = 501011;

    private const float BasicFlatReach = 2.0f;
    private const float BasicRadiusShare = 0.5f;

    private static readonly HashSet<int> FlatRadiiZones = new() { 45, 85, 89 };
    private static readonly HashSet<int> BasicFlatZones = new() { 29, 45, 57, 58, 59, 60, 85, 89 };
    private static readonly HashSet<int> FlatMeleeZones = new() { 45, 57, 58, 59, 60, 85, 89 };

    private static readonly HashSet<int> CrouchSkills = new() { 115808, 215808, 115580, 215580 };
    private static readonly HashSet<int> PullSkills = new() { 115570, 215570, 490826, 491322, 491323, 491324, 491325 };
    private static readonly HashSet<int> RushSkills = new() { 114509, 214509, 115509, 215509, 490825 };

    private static readonly HashSet<int> SoccerSkills = new()
    {
        101015, 102014, 103016, 104015, 105015, 106015, 107036, 108036, 109041, 110041, 111015, 112015,
        201015, 202011, 203016, 204015, 205015, 206015, 207036, 208036, 209041, 210041, 211015, 212015,
        113015, 114015, 115015, 213015, 214015, 215015,
        101016, 102015, 103017, 104016, 105016, 106016, 107037, 108037, 109042, 110042, 111016, 112016,
        201016, 202012, 203017, 204016, 205016, 206016, 207037, 208037, 209042, 210042, 211016, 212016,
        113016, 114016, 115016, 213016, 214016, 215016,
    };

    private static readonly HashSet<int> ZoneMeshOnlyZones = new() { 30, 61, 62, 63, 66 };

    public static bool IsSoccerSkill(int skillId) => SoccerSkills.Contains(skillId);

    public static bool SightChecked(int moral, bool targetHostile, int skillId) => moral switch
    {
        SkillTarget.NpcOnly or SkillTarget.All => true,
        SkillTarget.EnemyOnly => targetHostile && !IsSoccerSkill(skillId),
        SkillTarget.DeadFriendOnly => !targetHostile,
        _ => false,
    };

    public static bool ObjectsBlockSight(int zone) => !ZoneMeshOnlyZones.Contains(zone);

    public static int SpotKind(int skillId) =>
        RushSkills.Contains(skillId) ? SpotRush
        : PullSkills.Contains(skillId) ? SpotPull
        : CrouchSkills.Contains(skillId) ? SpotCrouch
        : SpotNone;

    public static int Kind(int type1, int type2) =>
        type1 == KindMelee || type2 == KindMelee ? KindMelee
        : type1 == KindRanged || type2 == KindRanged ? KindRanged
        : type1 == KindMagic || type2 == KindMagic ? KindMagic
        : KindNone;

    public static float BasicMelee(float casterRadius, float targetRadius, float weaponLength, int zone) =>
        BasicFlatZones.Contains(zone) || weaponLength <= 0f
            ? BasicFlatReach
            : (casterRadius + targetRadius) * BasicRadiusShare + weaponLength;

    public static float Radii(float casterRadius, float targetRadius, int zone) =>
        FlatRadiiZones.Contains(zone) ? FlatRadii : casterRadius + RadiusGap + targetRadius;

    public static SkillReachVerdict Check(in SkillReachQuery q)
    {
        if (q.SelfAnim2 == SiegeShellSelfAnim || q.SkillId == StatScrollSkill) return SkillReachVerdict.InRange;

        int kind = Kind(q.Type1, q.Type2);
        float radii = Radii(q.CasterRadius, q.TargetRadius, q.Zone);
        float d = q.Distance;

        if (q.Range > 0)
        {
            float reach = q.Range + radii;
            if (SpotKind(q.SkillId) == SpotRush && d <= reach)
                return d < RushMinimum ? SkillReachVerdict.TooNear : SkillReachVerdict.InRange;

            if (!SoccerSkills.Contains(q.SkillId)) reach += ExtraUnit;
            if (d <= reach)
            {
                if (!q.TargetIsPlayer) return SkillReachVerdict.InRange;
                if (kind == KindRanged)
                    return d <= RangedPlayerShortcut ? SkillReachVerdict.InRange : RangedWeaponCheck(q, radii);
                if (kind != KindMagic || d <= q.Range + MagicPlayerOverRange) return SkillReachVerdict.InRange;
                return SkillReachVerdict.TooFar;
            }
        }

        return kind switch
        {
            KindMelee => MeleeWeaponCheck(q, radii),
            KindRanged => RangedWeaponCheck(q, radii),
            _ => SkillReachVerdict.TooFar,
        };
    }

    private static SkillReachVerdict MeleeWeaponCheck(in SkillReachQuery q, float radii)
    {
        if (FlatMeleeZones.Contains(q.Zone))
            return q.Distance <= FlatMeleeReach ? SkillReachVerdict.InRange : SkillReachVerdict.OutOfReachSilent;
        float reach = q.MeleeWeaponReach + radii + ExtraUnit + q.MeleeReachBonus;
        return q.Distance <= reach ? SkillReachVerdict.InRange : SkillReachVerdict.TooFar;
    }

    private static SkillReachVerdict RangedWeaponCheck(in SkillReachQuery q, float radii)
    {
        if (q.RangedAddRangePercent <= 0) return SkillReachVerdict.OutOfReachSilent;
        float reach = (q.RangedWeaponReach + radii + ExtraUnit) * q.RangedAddRangePercent / PercentScale;
        if (q.Distance > reach) return SkillReachVerdict.TooFar;
        if (q.TargetIsPlayer && q.Distance > q.RangedWeaponReach + RangedPlayerShortcut) return SkillReachVerdict.TooFar;
        return SkillReachVerdict.InRange;
    }
}
