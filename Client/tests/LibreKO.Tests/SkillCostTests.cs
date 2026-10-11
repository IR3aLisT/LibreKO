using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class SkillCostTests
{
    [Theory]
    [InlineData(109, SkillCost.TextNotEnoughMp)]
    [InlineData(112, SkillCost.TextNotEnoughMp)]
    [InlineData(105, SkillCost.TextNotEnoughSp)]
    [InlineData(107, SkillCost.TextNotEnoughSp)]
    [InlineData(115, SkillCost.TextNotEnoughSp)]
    public void MagesAndPriestsLackMpOthersSp(int classCode, int textId) =>
        Assert.Equal(textId, SkillCost.ManaShortText(classCode));

    [Theory]
    [InlineData(100, 200, true)]
    [InlineData(300, 200, false)]
    [InlineData(100, 0, false)]
    [InlineData(100, SkillCost.HpCostCap, false)]
    public void AnHpCostUnderTheCapMustBeCovered(int hp, int cost, bool shortOfHp) =>
        Assert.Equal(shortOfHp, SkillCost.HpShort(hp, cost));
}
