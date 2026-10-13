using LibreKO.Domain;
using Xunit;
using V = LibreKO.Domain.SiegeSkills.Verdict;
using T = LibreKO.Domain.SiegeSkills.TargetKind;

namespace LibreKO.Tests;

public class SiegeSkillsTests
{
    private const int Delos = 30;
    private const int LunarWar = 62;
    private const int Oreads = 66;
    private const int Moradon = 21;
    private const int CatapultSkill = 450001;
    private const int LadderTruckSkill = 450003;
    private const int OtherBecomeSkill = 450010;
    private const int BecomeAnim = 111;
    private const int ShellAnim = 113;
    private const int RamAnim = 122;
    private const int CatapultLook = 30010;
    private const int RamLook = 30040;
    private const int TowerLook = 30050;
    private const int MonsterLook = 22010;
    private const int NormalAnim = 12;

    private static V Bare(int skill, int anim, int zone, bool onTower = false) =>
        SiegeSkills.Check(skill, anim, false, 0, onTower, zone, false, T.None);

    private static V InLook(int look, int skill, int anim, int zone, bool defender = false, T target = T.None) =>
        SiegeSkills.Check(skill, anim, true, look, false, zone, defender, target);

    [Fact]
    public void BecomeSkillsKeepToTheirZones()
    {
        Assert.Equal(V.Pass, Bare(CatapultSkill, BecomeAnim, Delos));
        Assert.Equal(V.Pass, Bare(CatapultSkill, BecomeAnim, LunarWar));
        Assert.Equal(V.Pass, Bare(CatapultSkill, BecomeAnim, Oreads));
        Assert.Equal(V.Failed, Bare(CatapultSkill, BecomeAnim, Moradon));
        Assert.Equal(V.Failed, Bare(LadderTruckSkill, BecomeAnim, Delos));
        Assert.Equal(V.Pass, Bare(LadderTruckSkill, BecomeAnim, Oreads));
        Assert.Equal(V.Pass, Bare(OtherBecomeSkill, BecomeAnim, Delos));
        Assert.Equal(V.Failed, Bare(OtherBecomeSkill, BecomeAnim, Oreads));
    }

    [Fact]
    public void WithoutALookOnlyBecomeSkillsPassAndNeverFromATower()
    {
        Assert.Equal(V.Failed, Bare(490101, ShellAnim, Delos));
        Assert.Equal(V.Failed, Bare(CatapultSkill, BecomeAnim, Delos, onTower: true));
        Assert.Equal(V.Pass, Bare(108010, NormalAnim, Moradon));
    }

    [Fact]
    public void EachLookTakesOnlyItsOwnBandAndNoBecomeSkill()
    {
        Assert.Equal(V.Failed, InLook(CatapultLook, CatapultSkill, BecomeAnim, Delos));
        Assert.Equal(V.Failed, InLook(CatapultLook, 108010, NormalAnim, Delos));
        Assert.Equal(V.Failed, InLook(TowerLook, 490101, ShellAnim, Delos));
        Assert.Equal(V.Pass, InLook(MonsterLook, 108010, NormalAnim, Moradon));
        Assert.Equal(V.Failed, InLook(MonsterLook, CatapultSkill, BecomeAnim, Delos));
    }

    [Fact]
    public void ShellsAreGatedByZoneAndWaterShellsByTheCastleSide()
    {
        Assert.Equal(V.Pass, InLook(CatapultLook, 490101, ShellAnim, LunarWar));
        Assert.Equal(V.Failed, InLook(CatapultLook, 490109, ShellAnim, Delos));
        Assert.Equal(V.Pass, InLook(CatapultLook, 490109, ShellAnim, Oreads));
        Assert.Equal(V.ZoneRefused, InLook(CatapultLook, 490139, ShellAnim, Oreads));
        Assert.Equal(V.NotDefender, InLook(CatapultLook, 490139, ShellAnim, Delos));
        Assert.Equal(V.Pass, InLook(SiegeTower.GuardTowerLook, 490139, ShellAnim, Delos, defender: true));
        Assert.Equal(V.Failed, InLook(CatapultLook, 490110, ShellAnim, LunarWar));
    }

    [Fact]
    public void TheRamOnlyStrikesHostileStructures()
    {
        Assert.Equal(V.Pass, InLook(RamLook, 450020, RamAnim, Delos, target: T.HostileStructure));
        Assert.Equal(V.Pass, InLook(RamLook, 450020, RamAnim, Delos));
        Assert.Equal(V.Failed, InLook(RamLook, 450020, RamAnim, Delos, target: T.Player));
        Assert.Equal(V.Failed, InLook(RamLook, 450020, RamAnim, Delos, target: T.OtherNpc));
    }
}
