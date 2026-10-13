using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class StrikeEffectTests
{
    private const int StanceBase = 100;
    private const int SwordKind = WeaponAnimation.Sword;

    private static int First(int count) => 0;
    private static int Last(int count) => count - 1;

    [Theory]
    [InlineData(12001, 12003)]
    [InlineData(32010, 32012)]
    [InlineData(0, StrikeEffect.None)]
    [InlineData(-1, StrikeEffect.None)]
    public void AGlowingWeaponHitsWithItsGlowPlusTwo(int glow, int expected)
    {
        Assert.Equal(expected, StrikeEffect.FromGlow(glow));
    }

    [Theory]
    [InlineData(false, SwordKind, StanceBase + 1, 10002, 10048)]
    [InlineData(false, SwordKind, StanceBase + 2, 10049, 10050)]
    [InlineData(false, SwordKind, 230, 10051, 10054)]
    [InlineData(true, SwordKind, 230, 10002, 10048)]
    [InlineData(false, WeaponAnimation.Staff, WeaponAnimation.AttackStaff, 10002, 10048)]
    [InlineData(false, SwordKind, WeaponAnimation.AttackStaff, 10051, 10054)]
    public void BloodFollowsTheAttackersClip(bool npc, int rightKind, int clip, int first, int last)
    {
        Assert.Equal(first, StrikeEffect.Blood(npc, rightKind, clip, StanceBase, First));
        Assert.Equal(last, StrikeEffect.Blood(npc, rightKind, clip, StanceBase, Last));
    }
}
