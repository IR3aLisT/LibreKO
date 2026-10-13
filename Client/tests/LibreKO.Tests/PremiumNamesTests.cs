using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class PremiumNamesTests
{
    [Theory]
    [InlineData(1, 1479)]
    [InlineData(5, 1483)]
    [InlineData(8, 1486)]
    [InlineData(10, 1487)]
    [InlineData(12, 1489)]
    public void TypesTakeTheRetailNameTexts(int type, int text)
    {
        Assert.Equal(text, PremiumNames.Name(type)!.Value.Text);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(13)]
    public void TypesWithoutARetailNameShowOnlyPremium(int type)
    {
        Assert.Null(PremiumNames.Name(type));
    }

    [Theory]
    [InlineData(0, PremiumNames.LastDayText, 0)]
    [InlineData(5, PremiumNames.HoursText, 5)]
    [InlineData(23, PremiumNames.HoursText, 23)]
    [InlineData(24, PremiumNames.DaysText, 1)]
    [InlineData(80, PremiumNames.DaysText, 3)]
    public void TheTimeLineUsesHoursUnderADayThenWholeDays(int hours, int text, int value)
    {
        var (id, _, shown) = PremiumNames.TimeLeft(hours);
        Assert.Equal(text, id);
        Assert.Equal(value, shown);
    }
}
