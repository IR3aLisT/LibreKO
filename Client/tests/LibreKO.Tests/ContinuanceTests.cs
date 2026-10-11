using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class ContinuanceTests
{
    [Theory]
    [InlineData(111654, Continuance.BuffType, 0, 2, false, ContinuanceCheck.Send)]
    [InlineData(111654, 0, Continuance.BuffType, 2, false, ContinuanceCheck.Send)]
    [InlineData(111654, 3, 0, 6, false, ContinuanceCheck.Send)]
    [InlineData(111654, 3, 0, 7, false, ContinuanceCheck.Cannot)]
    [InlineData(500100, Continuance.BuffType, 0, 2, false, ContinuanceCheck.Skip)]
    [InlineData(111654, Continuance.BuffType, 0, 2, true, ContinuanceCheck.Already)]
    public void OnlyRunningClassBuffsCanBeDoubledOnce(int skillId, int type1, int type2, int moral, bool extended, ContinuanceCheck check)
    {
        Assert.Equal(check, Continuance.Check(skillId, type1, type2, moral, extended));
    }

    [Fact]
    public void ContinuanceItemsAreRecognisedByTheirMoral()
    {
        Assert.True(Continuance.IsContinuanceItem(Continuance.Moral));
        Assert.False(Continuance.IsContinuanceItem(1));
    }
}
