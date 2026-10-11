using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class CooldownGroupsTests
{
    [Theory]
    [InlineData(CooldownGroups.None, false)]
    [InlineData(1, false)]
    [InlineData(CooldownGroups.Shells, true)]
    [InlineData(CooldownGroups.SiegeWeapons, true)]
    [InlineData(4, false)]
    [InlineData(5, false)]
    public void OnlyShellsAndSiegeWeaponsShareACooldown(int group, bool shares)
    {
        Assert.Equal(shares, CooldownGroups.Shares(group));
    }

    [Theory]
    [InlineData(0.2, 1)]
    [InlineData(3.0, 4)]
    [InlineData(3.9, 4)]
    public void TheRefusalCountsTheRemainingSecondsPlusOne(double remaining, int shown)
    {
        Assert.Equal(shown, CooldownGroups.SecondsShown(remaining));
    }
}
