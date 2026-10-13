using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class SkillReachTests
{
    private const int MeleeSkill = 101001;
    private const int RushSkill = 114509;
    private const int SoccerSkill = 101015;
    private const int OpenZone = 21;
    private const int ChaosZone = 85;
    private const int FlatMeleeOnlyZone = 57;
    private const int FlatBasicOnlyZone = 29;

    private static SkillReachQuery Melee(float distance, int zone = OpenZone, float bonus = 0f) => new()
    {
        SkillId = MeleeSkill,
        Type1 = SkillReach.KindMelee,
        Distance = distance,
        CasterRadius = 0.5f,
        TargetRadius = 0.9f,
        Zone = zone,
        MeleeWeaponReach = 0.5f,
        MeleeReachBonus = bonus,
    };

    [Theory]
    [InlineData(3.8f, SkillReachVerdict.InRange)]
    [InlineData(3.9f, SkillReachVerdict.InRange)]
    [InlineData(4.0f, SkillReachVerdict.TooFar)]
    public void AMeleeSkillReachesWeaponPlusBothRadiiPlusTwoUnits(float distance, SkillReachVerdict expected) =>
        Assert.Equal(expected, SkillReach.Check(Melee(distance)));

    [Fact]
    public void TheReachBuffAddsItsAmountToMeleeReach() =>
        Assert.Equal(SkillReachVerdict.InRange, SkillReach.Check(Melee(4.5f, bonus: 1f)));

    [Theory]
    [InlineData(ChaosZone, 5.0f, SkillReachVerdict.InRange)]
    [InlineData(ChaosZone, 5.1f, SkillReachVerdict.OutOfReachSilent)]
    [InlineData(FlatMeleeOnlyZone, 5.1f, SkillReachVerdict.OutOfReachSilent)]
    public void UniformEventZonesUseAFlatFiveUnitMeleeReachAndRefuseQuietly(int zone, float distance, SkillReachVerdict expected) =>
        Assert.Equal(expected, SkillReach.Check(Melee(distance, zone)));

    [Fact]
    public void FlatRadiiApplyOnlyInTheirOwnZones()
    {
        Assert.Equal(2.0f, SkillReach.Radii(3f, 3f, ChaosZone));
        Assert.Equal(7.0f, SkillReach.Radii(3f, 3f, FlatMeleeOnlyZone));
    }

    private static SkillReachQuery Rush(float distance) => new()
    {
        SkillId = RushSkill,
        Type1 = MagicType.Warp,
        Range = 15,
        Distance = distance,
        CasterRadius = 0.5f,
        TargetRadius = 0.5f,
        Zone = OpenZone,
        TargetIsPlayer = true,
    };

    [Theory]
    [InlineData(4.9f, SkillReachVerdict.TooNear)]
    [InlineData(5.0f, SkillReachVerdict.InRange)]
    [InlineData(17.0f, SkillReachVerdict.InRange)]
    [InlineData(18.0f, SkillReachVerdict.InRange)]
    [InlineData(18.5f, SkillReachVerdict.TooFar)]
    public void RushRefusesUnderFiveUnitsInsideItsReach(float distance, SkillReachVerdict expected) =>
        Assert.Equal(expected, SkillReach.Check(Rush(distance)));

    private static SkillReachQuery Magic(int skillId, int range, float distance, bool player, float radius = 0.5f) => new()
    {
        SkillId = skillId,
        Type1 = SkillReach.KindMagic,
        Range = range,
        Distance = distance,
        CasterRadius = radius,
        TargetRadius = radius,
        Zone = OpenZone,
        TargetIsPlayer = player,
    };

    [Fact]
    public void ATableRangeAddsTheRadiiAndOneUnit()
    {
        Assert.Equal(SkillReachVerdict.InRange, SkillReach.Check(Magic(110503, 20, 23.0f, player: false)));
        Assert.Equal(SkillReachVerdict.TooFar, SkillReach.Check(Magic(110503, 20, 23.1f, player: false)));
    }

    [Fact]
    public void ListedSkillsDropTheExtraUnit() =>
        Assert.Equal(SkillReachVerdict.TooFar, SkillReach.Check(Magic(SoccerSkill, 20, 22.5f, player: false)));

    [Fact]
    public void MagicAgainstAPlayerIsCappedAtRangePlusFifteen()
    {
        Assert.Equal(SkillReachVerdict.InRange, SkillReach.Check(Magic(110503, 1, 16.0f, player: true, radius: 10f)));
        Assert.Equal(SkillReachVerdict.TooFar, SkillReach.Check(Magic(110503, 1, 16.5f, player: true, radius: 10f)));
        Assert.Equal(SkillReachVerdict.InRange, SkillReach.Check(Magic(110503, 1, 16.5f, player: false, radius: 10f)));
    }

    private static SkillReachQuery Ranged(float distance, bool player, int addRange = 150, int range = 0) => new()
    {
        SkillId = 108505,
        Type1 = SkillReach.KindRanged,
        Range = range,
        Distance = distance,
        CasterRadius = 0.5f,
        TargetRadius = 0.5f,
        Zone = OpenZone,
        TargetIsPlayer = player,
        RangedWeaponReach = 40f,
        RangedAddRangePercent = addRange,
    };

    [Fact]
    public void ARangedSkillUsesTheBowReachTimesItsAddRange()
    {
        Assert.Equal(SkillReachVerdict.InRange, SkillReach.Check(Ranged(64.0f, player: false)));
        Assert.Equal(SkillReachVerdict.TooFar, SkillReach.Check(Ranged(64.6f, player: false)));
    }

    [Fact]
    public void ARangedSkillAgainstAPlayerStopsAtTheBowPlusThirtyEight()
    {
        Assert.Equal(SkillReachVerdict.InRange, SkillReach.Check(Ranged(64.0f, player: true)));
        Assert.Equal(SkillReachVerdict.InRange, SkillReach.Check(Ranged(80f, player: false, addRange: 200)));
        Assert.Equal(SkillReachVerdict.TooFar, SkillReach.Check(Ranged(80f, player: true, addRange: 200)));
    }

    [Fact]
    public void ARangedSkillWithoutItsTypeTwoRowIsRefusedQuietly() =>
        Assert.Equal(SkillReachVerdict.OutOfReachSilent, SkillReach.Check(Ranged(10f, player: false, addRange: 0)));

    [Fact]
    public void ARangedTableReachAgainstAPlayerPastThirtyEightFallsBackToTheBow()
    {
        Assert.Equal(SkillReachVerdict.InRange, SkillReach.Check(Ranged(45f, player: true, range: 50)));
        Assert.Equal(SkillReachVerdict.TooFar, SkillReach.Check(Ranged(45f, player: true, addRange: 50, range: 50)));
    }

    [Theory]
    [InlineData(113, 0)]
    [InlineData(0, 501011)]
    public void SomeSkillsAreAlwaysInRange(int selfAnim2, int skillId) =>
        Assert.Equal(SkillReachVerdict.InRange, SkillReach.Check(Melee(1000f) with { SelfAnim2 = selfAnim2, SkillId = skillId == 0 ? MeleeSkill : skillId }));

    [Theory]
    [InlineData(114509, SkillReach.SpotRush)]
    [InlineData(490825, SkillReach.SpotRush)]
    [InlineData(115570, SkillReach.SpotPull)]
    [InlineData(491324, SkillReach.SpotPull)]
    [InlineData(115808, SkillReach.SpotCrouch)]
    [InlineData(101001, SkillReach.SpotNone)]
    public void SpotKindsFollowTheRetailLists(int skillId, int kind) =>
        Assert.Equal(kind, SkillReach.SpotKind(skillId));
    [Theory]
    [InlineData(SkillTarget.NpcOnly, true, MeleeSkill, true)]
    [InlineData(SkillTarget.All, false, MeleeSkill, true)]
    [InlineData(SkillTarget.EnemyOnly, true, MeleeSkill, true)]
    [InlineData(SkillTarget.EnemyOnly, false, MeleeSkill, false)]
    [InlineData(SkillTarget.EnemyOnly, true, SoccerSkill, false)]
    [InlineData(SkillTarget.DeadFriendOnly, false, MeleeSkill, true)]
    [InlineData(SkillTarget.DeadFriendOnly, true, MeleeSkill, false)]
    [InlineData(SkillTarget.Party, false, MeleeSkill, false)]
    [InlineData(SkillTarget.AreaEnemy, true, MeleeSkill, false)]
    public void SightIsCheckedForTheRetailMorals(int moral, bool hostile, int skillId, bool expected) =>
        Assert.Equal(expected, SkillReach.SightChecked(moral, hostile, skillId));

    [Theory]
    [InlineData(1.0f, 1.0f, 1.2f, OpenZone, 2.2f)]
    [InlineData(1.0f, 3.0f, 1.2f, OpenZone, 3.2f)]
    [InlineData(1.0f, 1.0f, 0f, OpenZone, 2.0f)]
    [InlineData(1.0f, 3.0f, 1.2f, ChaosZone, 2.0f)]
    [InlineData(1.0f, 3.0f, 1.2f, FlatBasicOnlyZone, 2.0f)]
    public void BasicMeleeReachIsHalfBothRadiiPlusTheWeapon(float self, float target, float weapon, int zone, float expected) =>
        Assert.Equal(expected, SkillReach.BasicMelee(self, target, weapon, zone), 3);

    [Theory]
    [InlineData(21, true)]
    [InlineData(30, false)]
    [InlineData(61, false)]
    [InlineData(66, false)]
    public void WarZonesTestOnlyTheZoneMesh(int zone, bool objectsBlock) =>
        Assert.Equal(objectsBlock, SkillReach.ObjectsBlockSight(zone));
}
