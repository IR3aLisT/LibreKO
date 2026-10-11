using System.Collections.Generic;
using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class QuestJournalRulesTests
{
    [Fact]
    public void AnywhereComesLastAndReadyQuestsLeadTheirZone()
    {
        var rows = new List<QuestJournalRules.Row>
        {
            new(1, 1, QuestJournalRules.AnywhereZone, 10),
            new(2, 1, 21, 30),
            new(3, 3, 21, 50),
            new(4, 1, 21, 20),
            new(5, 1, 1, 60),
        };

        rows.Sort(QuestJournalRules.Compare);

        Assert.Equal(new[] { 5, 3, 4, 2, 1 }, rows.ConvertAll(r => r.QuestId));
    }

    [Theory]
    [InlineData(999, "999")]
    [InlineData(1_500, "1.5K")]
    [InlineData(125_400, "125.4K")]
    [InlineData(2_500_000, "2.5M")]
    [InlineData(3_000_000_000, "3B")]
    public void LargeRewardCountsAreCompact(long count, string text)
    {
        Assert.Equal(text, QuestJournalRules.CompactCount(count));
    }

    [Fact]
    public void SearchMatchesAnyPartOfTheName()
    {
        Assert.True(QuestJournalRules.Matches("Worm hunt", "WORM"));
        Assert.True(QuestJournalRules.Matches("Worm hunt", ""));
        Assert.False(QuestJournalRules.Matches("Worm hunt", "bulcan"));
    }

    [Fact]
    public void TheStatusLineLeadsWithTheLevelWhenThereIsOne()
    {
        Assert.Equal("Lv. 35  ·  In progress", QuestJournalRules.StatusLine(35, "In progress"));
        Assert.Equal("Ready to claim", QuestJournalRules.StatusLine(0, "Ready to claim"));
        Assert.Equal("Moradon  (2)", QuestJournalRules.GroupTitle("Moradon", 2));
    }

    [Fact]
    public void ObjectiveBarsStopAtFull()
    {
        Assert.Equal(0.65f, QuestJournalRules.Fraction(13, 20));
        Assert.Equal(1f, QuestJournalRules.Fraction(12, 10));
        Assert.Equal(0f, QuestJournalRules.Fraction(3, 0));
    }
}
