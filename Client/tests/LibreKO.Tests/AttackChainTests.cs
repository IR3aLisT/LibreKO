using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class AttackChainTests
{
    private const double Start = 10.0;
    private const double Quick = 0.5;
    private const double Slow = 0.9;

    [Fact]
    public void ASkillRightAfterTheAttackStartsChains()
    {
        var chain = new AttackChain();
        Assert.False(chain.Record(AttackChain.BasicAttack, Start));
        Assert.True(chain.Record(AttackChain.InstantSkill, Start + Quick));
    }

    [Fact]
    public void ASlowFollowUpDoesNotChain()
    {
        var chain = new AttackChain();
        chain.Record(AttackChain.InstantSkill, Start);
        Assert.False(chain.Record(AttackChain.BasicAttack, Start + Slow));
    }

    [Fact]
    public void AttackAfterAttackNeverChains()
    {
        var chain = new AttackChain();
        chain.Record(AttackChain.BasicAttack, Start);
        Assert.False(chain.Record(AttackChain.BasicAttack, Start + Quick));
    }

    [Fact]
    public void TheFlagIsSpentOnce()
    {
        var chain = new AttackChain();
        chain.Record(AttackChain.BasicAttack, Start);
        chain.Record(AttackChain.InstantSkill, Start + Quick);
        Assert.True(chain.Consume());
        Assert.False(chain.Consume());
    }

    [Theory]
    [InlineData(0u, 70128)]
    [InlineData(1u, 70129)]
    [InlineData(5u, 70130)]
    public void TheEffectIsOneOfTheThreeChainBursts(uint roll, int expected) =>
        Assert.Equal(expected, AttackChain.Effect(roll));
}
