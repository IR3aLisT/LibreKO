using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class BuffExpiryTests
{
    [Theory]
    [InlineData(BuffExpiry.DamageOverTimeCured, 0, MagicType.DotHeal, -30, true)]
    [InlineData(BuffExpiry.DamageOverTimeCured, 0, MagicType.DotHeal, 40, false)]
    [InlineData(BuffExpiry.HealOverTimeEnded, 0, MagicType.DotHeal, 40, true)]
    [InlineData(BuffExpiry.HealOverTimeEnded, 0, MagicType.DotHeal, -30, false)]
    [InlineData(BuffExpiry.DamageOverTimeCured, 0, MagicType.Buff, -30, false)]
    [InlineData(6, 6, MagicType.Buff, 0, true)]
    [InlineData(6, 7, MagicType.Buff, 0, false)]
    [InlineData(0, 0, MagicType.Buff, 0, false)]
    public void TheExpiryCodePicksTheChipsItRemoves(int code, int buffType, int type1, int timeDamage, bool removed)
    {
        Assert.Equal(removed, BuffExpiry.Removes(code, buffType, type1, timeDamage));
    }

    [Theory]
    [InlineData(BuffExpiry.StealthEnded, StealthKind.DispelOnMove, true)]
    [InlineData(BuffExpiry.StealthEnded, StealthKind.DispelOnAttack, true)]
    [InlineData(BuffExpiry.StealthEnded, StealthKind.SeeInvisible, false)]
    [InlineData(BuffExpiry.SightEnded, StealthKind.SeeInvisible, true)]
    [InlineData(BuffExpiry.SightEnded, StealthKind.SeeInvisibleParty, true)]
    [InlineData(BuffExpiry.SightEnded, StealthKind.DispelOnMove, false)]
    [InlineData(BuffExpiry.SummonEnded, StealthKind.PetSummon, true)]
    [InlineData(BuffExpiry.SummonEnded, StealthKind.ChiefGuardian, true)]
    [InlineData(BuffExpiry.SummonEnded, StealthKind.DispelOnAttack, false)]
    public void StateChangeSkillsEndByTheirKind(int code, int stateChange, bool removed)
    {
        Assert.Equal(removed, BuffExpiry.Removes(code, 0, MagicType.Stealth, 0, stateChange));
    }

    [Fact]
    public void StealthCodesLeaveOtherSkillTypesToTheirBuffType()
    {
        Assert.False(BuffExpiry.Removes(BuffExpiry.StealthEnded, 0, MagicType.Buff, 0, StealthKind.DispelOnMove));
    }

    [Theory]
    [InlineData(30.7, false)]
    [InlineData(10.7, false)]
    [InlineData(9.7, true)]
    [InlineData(9.2, false)]
    [InlineData(0.6, true)]
    [InlineData(0.4, false)]
    public void ABuffDimsHalfOfEachSecondInItsLastTen(double left, bool dimmed) =>
        Assert.Equal(dimmed, BuffExpiry.BlinkDimmed(left));
}
