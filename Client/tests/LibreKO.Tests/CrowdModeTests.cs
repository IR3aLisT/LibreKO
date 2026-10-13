using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class CrowdModeTests
{
    [Fact]
    public void HighQualityLimitsNothing() =>
        Assert.Equal(CrowdModes.Unlimited, CrowdModes.Of(CrowdMode.Quality).StrangerFxPerSecond);

    [Fact]
    public void FpsBoostIsStricterThanBalanced()
    {
        var balanced = CrowdModes.Of(CrowdMode.Balanced);
        var boost = CrowdModes.Of(CrowdMode.Boost);
        Assert.True(boost.StrangerFxPerSecond < balanced.StrangerFxPerSecond);
        Assert.True(boost.StrangerFxRange < balanced.StrangerFxRange);
        Assert.True(boost.HoverHz < balanced.HoverHz);
    }

    [Fact]
    public void TheBudgetAllowsOneSecondsWorthThenRefills()
    {
        var budget = new RateBudget();
        int taken = 0;
        for (int i = 0; i < 10; i++)
            if (budget.Take(4, 0.0)) taken++;
        Assert.Equal(4, taken);
        Assert.False(budget.Take(4, 0.1));
        Assert.True(budget.Take(4, 0.5));
    }

    [Fact]
    public void AnUnlimitedBudgetNeverRefuses()
    {
        var budget = new RateBudget();
        for (int i = 0; i < 100; i++)
            Assert.True(budget.Take(CrowdModes.Unlimited, 0.0));
    }
}
