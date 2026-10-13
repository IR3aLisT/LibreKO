using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class OnlineTimeTests
{
    [Theory]
    [InlineData(0, "0 s")]
    [InlineData(59, "59 s")]
    [InlineData(60, "1 min")]
    [InlineData(3599, "59 min")]
    [InlineData(3600, "1 h 0 min")]
    [InlineData(46_740, "12 h 59 min")]
    [InlineData(-5, "0 s")]
    public void OnlineTimeReadsInHoursMinutesOrSeconds(long seconds, string text) =>
        Assert.Equal(text, OnlineTime.Format(seconds));
}
