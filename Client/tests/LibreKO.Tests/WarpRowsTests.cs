using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class WarpRowsTests
{
    [Theory]
    [InlineData(30, 100_000, 35, 83, 5_000, "Requires level 35.")]
    [InlineData(84, 100_000, 35, 83, 5_000, "Level 83 and below only.")]
    [InlineData(40, 1_000, 35, 83, 5_000, "Need 5,000 gold.")]
    [InlineData(40, 100_000, 35, 83, 5_000, null)]
    public void ARowIsBlockedByLevelThenByGold(int level, long gold, int min, int max, long fee, string? reason) =>
        Assert.Equal(reason, WarpRows.BlockReason(level, gold, min, max, fee));

    [Fact]
    public void FeesAndLevelsReadShort()
    {
        Assert.Equal("Free", WarpRows.FeeText(0));
        Assert.Equal("17,000 gold", WarpRows.FeeText(17_000));
        Assert.Equal("lv35  ~  lv83", WarpRows.LevelText(35, 83));
        Assert.Equal("lv45+", WarpRows.LevelText(45, 0));
        Assert.Equal("", WarpRows.LevelText(0, 0));
    }

    [Fact]
    public void TheFirstUsableRowIsPreselected()
    {
        Assert.Equal(2, WarpRows.FirstUsable(new[] { "Requires level 45.", "Need 5,000 gold.", null, null }));
        Assert.Equal(0, WarpRows.FirstUsable(new[] { "Requires level 45." }));
    }
}
