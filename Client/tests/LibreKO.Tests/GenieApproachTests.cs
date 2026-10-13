using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class GenieApproachTests
{
    [Fact]
    public void RAttackClosesInWhileTheAttackSkillWaitsInReach()
    {
        Assert.True(GenieApproach.ShouldMove(huntInPlace: false, basicAttack: true, basicInReach: false, hasAttackSkill: true, attackSkillInReach: true));
    }

    [Fact]
    public void RAttackStaysPutOnceTheMonsterIsInBasicReach()
    {
        Assert.False(GenieApproach.ShouldMove(huntInPlace: false, basicAttack: true, basicInReach: true, hasAttackSkill: true, attackSkillInReach: true));
        Assert.False(GenieApproach.ShouldMove(huntInPlace: false, basicAttack: true, basicInReach: true, hasAttackSkill: false, attackSkillInReach: false));
    }

    [Fact]
    public void RAttackAloneWalksToAMonsterOutOfReach()
    {
        Assert.True(GenieApproach.ShouldMove(huntInPlace: false, basicAttack: true, basicInReach: false, hasAttackSkill: false, attackSkillInReach: false));
    }

    [Fact]
    public void SkillsAloneStopAtTheSkillsReach()
    {
        Assert.True(GenieApproach.ShouldMove(huntInPlace: false, basicAttack: false, basicInReach: false, hasAttackSkill: true, attackSkillInReach: false));
        Assert.False(GenieApproach.ShouldMove(huntInPlace: false, basicAttack: false, basicInReach: false, hasAttackSkill: true, attackSkillInReach: true));
    }

    [Fact]
    public void NothingToAttackWithMeansNoApproach()
    {
        Assert.False(GenieApproach.ShouldMove(huntInPlace: false, basicAttack: false, basicInReach: false, hasAttackSkill: false, attackSkillInReach: false));
    }

    [Fact]
    public void HuntingInPlaceNeverWalks()
    {
        Assert.False(GenieApproach.ShouldMove(huntInPlace: true, basicAttack: true, basicInReach: false, hasAttackSkill: true, attackSkillInReach: false));
        Assert.False(GenieApproach.ShouldMove(huntInPlace: true, basicAttack: false, basicInReach: false, hasAttackSkill: true, attackSkillInReach: false));
    }
}
