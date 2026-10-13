using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class VipVaultAccessTests
{
    [Theory]
    [InlineData(21)]
    [InlineData(71)]
    public void TheVaultOpensInOrdinaryZones(int zone)
    {
        Assert.True(VipVaultAccess.ButtonOpens(zone));
        Assert.True(VipVaultAccess.CommandOpens(zone));
    }

    [Theory]
    [InlineData(45)]
    [InlineData(84)]
    [InlineData(85)]
    public void EventZonesKeepTheVaultShut(int zone)
    {
        Assert.False(VipVaultAccess.ButtonOpens(zone));
        Assert.False(VipVaultAccess.CommandOpens(zone));
    }

    [Fact]
    public void TheSecondBorderWarMapShutsOnlyTheBagButton()
    {
        Assert.False(VipVaultAccess.ButtonOpens(89));
        Assert.True(VipVaultAccess.CommandOpens(89));
    }
}
