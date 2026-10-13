using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class RebirthProgressTests
{
    [Fact]
    public void ProgressCountsOnlyAtTheLevelCap()
    {
        Assert.Equal(0f, RebirthProgress.Fraction(80, 50, 0));
        Assert.Equal(0.5f, RebirthProgress.Fraction(CharacterSheet.MaxLevel, 50, 0));
        Assert.Equal(1f, RebirthProgress.Fraction(1, 0, RebirthPick.MaxRebirthLevel));
    }

    [Fact]
    public void ReadyNeedsFullExperienceBelowTheRebirthCap()
    {
        Assert.True(RebirthProgress.Ready(CharacterSheet.MaxLevel, 100, 3));
        Assert.False(RebirthProgress.Ready(CharacterSheet.MaxLevel, 99.9, 3));
        Assert.False(RebirthProgress.Ready(CharacterSheet.MaxLevel, 100, RebirthPick.MaxRebirthLevel));
    }

    [Fact]
    public void TheTooltipListsTheRebirthPoints()
    {
        int[] bonus = { 2, 0, 2, 0, 0 };
        string text = RebirthProgress.Tooltip(CharacterSheet.MaxLevel, 64, 2, row => bonus[row]);

        Assert.Equal("Rebirth 2 — 64.0% to the next rebirth\nRebirth points: 4 (STR +2, DEX +2)", text);
    }
}
