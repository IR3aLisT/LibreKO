using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class UserInfoCardTests
{
    [Theory]
    [InlineData(1, 4, UserInfoCard.ClanGradeText, 4)]
    [InlineData(2, 3, UserInfoCard.TrainingKnightText, 3)]
    [InlineData(3, 1, UserInfoCard.AccreditedKnightText, 5)]
    [InlineData(7, 1, UserInfoCard.AccreditedKnightText, 1)]
    [InlineData(8, 1, UserInfoCard.RoyalKnightText, 5)]
    [InlineData(12, 1, UserInfoCard.RoyalKnightText, 1)]
    [InlineData(0, 1, 0, 0)]
    public void TheClanGradeFollowsTheFlag(int flag, int grade, int text, int number) =>
        Assert.Equal((text, number), UserInfoCard.ClanGrade(flag, grade));

    [Fact]
    public void TheRankRowsSkipBoardThreeAndTheUnrankedAdonisValue()
    {
        var rows = UserInfoCard.Rows(new[] { 4, 255, 0, 99, 12, 0, 1 });

        Assert.Equal(6, rows.Count);
        Assert.Equal(new UserRankRow(1, 0), rows[1]);
        Assert.DoesNotContain(rows, r => r.Board == UserInfoCard.UnusedBoard);
        Assert.Equal(new UserRankRow(6, 1), rows[5]);
    }

    [Fact]
    public void TheSummaryShowsTheBestTierAndSkipsEmptyMarks()
    {
        var cells = UserInfoCard.Summary(new[]
        {
            new RankMark(0, 2, 1, 0),
            new RankMark(1, 0, 0, 0),
            new RankMark(52, 0, 0, 3),
            new RankMark(2, 5, 0, 0),
        });

        Assert.Equal(new[] { new RankMarkCell(0, 1, 1), new RankMarkCell(52, 2, 3), new RankMarkCell(2, 0, 5) }, cells);
    }

    [Theory]
    [InlineData(0, 2, 1, "simple_rank_mark_0_2")]
    [InlineData(51, 2, 4, "simple_rank_mark_51")]
    [InlineData(4, 1, 0, "simple_rank_mark_99")]
    public void TheRibbonNamesMatchTheBakedArt(int type, int tier, int count, string name) =>
        Assert.Equal(name, UserInfoCard.Ribbon(type, tier, count));

    [Theory]
    [InlineData(2, 2, 2)]
    [InlineData(6, 1, 6)]
    [InlineData(6, 4, 4)]
    [InlineData(0, 3, 0)]
    [InlineData(5, 7, 7)]
    public void TheNearbyStateFollowsTheTwoSignalBytes(byte kind, byte signal, int state) =>
        Assert.Equal(state, UserInfoCard.NearbyState(signal, kind));

    [Theory]
    [InlineData(7, UserInfoCard.GatheringText)]
    [InlineData(2, UserInfoCard.MerchantText)]
    [InlineData(6, UserInfoCard.MerchantText)]
    [InlineData(0, UserInfoCard.OnlineText)]
    public void TheStateText(int state, int text) => Assert.Equal(text, UserInfoCard.StateText(state));

    [Theory]
    [InlineData(1, 1f, 0.843f, 0f)]
    [InlineData(10, 1f, 1f, 0f)]
    [InlineData(1001, 1f, 1f, 1f)]
    public void ThePlaceColour(int place, float r, float g, float b)
    {
        var colour = UserInfoCard.PlaceColour(place);
        Assert.Equal(r, colour.R, 2);
        Assert.Equal(g, colour.G, 2);
        Assert.Equal(b, colour.B, 2);
    }

    [Fact]
    public void LargeNumbersGetThousandSeparators() => Assert.Equal("1,234,567", UserInfoCard.Thousands(1_234_567));

    [Fact]
    public void TheRelationCombinesFriendAndClan()
    {
        Assert.Equal(UserRelation.Both, UserInfoCard.Relation(true, true));
        Assert.Equal(UserRelation.Clan, UserInfoCard.Relation(false, true));
        Assert.Equal(UserRelation.None, UserInfoCard.Relation(false, false));
    }

    [Theory]
    [InlineData(7, 43619)]
    [InlineData(0, 43620)]
    [InlineData(6, 43626)]
    [InlineData(3, 0)]
    [InlineData(54, 43631)]
    public void TheMarkTooltips(int type, int text) => Assert.Equal(text, UserInfoCard.MarkTooltip(type));
}
