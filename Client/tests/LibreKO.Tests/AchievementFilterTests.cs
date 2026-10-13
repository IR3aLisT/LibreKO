using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class AchievementFilterTests
{
    [Fact]
    public void SearchLooksAtTheNameAndTheObjective()
    {
        Assert.True(AchievementFilter.Matches("Revenge Taker", "Succeed in Revenge x 1", "taker"));
        Assert.True(AchievementFilter.Matches("Revenge Taker", "Succeed in Revenge x 1", " succeed "));
        Assert.False(AchievementFilter.Matches("Revenge Taker", "Succeed in Revenge x 1", "arena"));
        Assert.True(AchievementFilter.Matches("Revenge Taker", "Succeed in Revenge x 1", ""));
    }

    [Fact]
    public void TheStatusFilterSplitsDoneFromUndone()
    {
        Assert.True(AchievementFilter.Passes(AchievementStatusFilter.All, completed: false));
        Assert.True(AchievementFilter.Passes(AchievementStatusFilter.InProgress, completed: false));
        Assert.False(AchievementFilter.Passes(AchievementStatusFilter.InProgress, completed: true));
        Assert.True(AchievementFilter.Passes(AchievementStatusFilter.Completed, completed: true));
        Assert.False(AchievementFilter.Passes(AchievementStatusFilter.Completed, completed: false));
    }

    [Theory]
    [InlineData(1, AchievementTab.Normal)]
    [InlineData(12, AchievementTab.Quest)]
    [InlineData(25, AchievementTab.War)]
    [InlineData(32, AchievementTab.Adventure)]
    [InlineData(41, AchievementTab.Challenge)]
    public void ACategoryBelongsToTheTabOfItsTens(int group, AchievementTab tab)
    {
        Assert.Equal(tab, AchievementData.TabOfGroup(group));
    }
}
