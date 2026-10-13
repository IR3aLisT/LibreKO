using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class SkillUsableTests
{
    private const int NoItem = 0;
    private const int Sword = 21;
    private const int Bow = 70;
    private const int SwordClass = 2;
    private const int BowClass = 7;
    private const int CryEcho = 106782;
    private const int BattleCry = 106781;

    [Theory]
    [InlineData(SkillUsable.NeedsNoWeapon)]
    [InlineData(SkillUsable.AnyHands)]
    public void SomeSkillsNeedNoWeaponAtAll(int need) =>
        Assert.True(SkillUsable.GearAllows(need, NoItem, NoItem));

    [Fact]
    public void AnyWeaponSkillsNeedSomeWeapon()
    {
        Assert.False(SkillUsable.GearAllows(SkillUsable.AnyWeapon, NoItem, NoItem));
        Assert.True(SkillUsable.GearAllows(SkillUsable.AnyWeapon, NoItem, Sword));
    }

    [Fact]
    public void AClassSkillNeedsThatClassInEitherHand()
    {
        Assert.True(SkillUsable.GearAllows(BowClass, NoItem, Bow));
        Assert.False(SkillUsable.GearAllows(BowClass, Sword, NoItem));
        Assert.True(SkillUsable.GearAllows(SwordClass, Sword, NoItem));
    }

    [Fact]
    public void AJamadarCountsAsADagger() =>
        Assert.Equal(1, SkillUsable.WeaponClass(SkillUsable.JamadarKind));

    [Fact]
    public void AngerExplosionWaitsForAFullGauge()
    {
        Assert.False(SkillUsable.AngerAllows(105255, gaugeFull: false));
        Assert.True(SkillUsable.AngerAllows(105255, gaugeFull: true));
        Assert.True(SkillUsable.AngerAllows(CryEcho, gaugeFull: false));
    }

    [Fact]
    public void CryEchoWaitsForBattleCry()
    {
        Assert.False(SkillUsable.BuffAllows(BattleCry, _ => false));
        Assert.True(SkillUsable.BuffAllows(BattleCry, id => id == BattleCry));
        Assert.True(SkillUsable.BuffAllows(0, _ => false));
    }
}
