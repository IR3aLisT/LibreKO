using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class ClanBonusCardTests
{
    [Theory]
    [InlineData(3, "X X X X X X")]
    [InlineData(5, "O X X X X X")]
    [InlineData(24, "O O O X X X")]
    [InlineData(70, "O O O O O O")]
    public void TheMarksLightOnePerTenOnlineFromFive(int online, string marks)
    {
        Assert.Equal(marks, ClanBonusCard.Marks(online, "O", "X"));
    }

    [Fact]
    public void TheTooltipNamesTheThresholdOrTheBonus()
    {
        Assert.Equal("need 5", ClanBonusCard.Tooltip(2, "need %d", "EXP %d%% NP +%d"));
        Assert.Equal("EXP 39% NP +3", ClanBonusCard.Tooltip(24, "need %d", "EXP %d%% NP +%d"));
    }
}
