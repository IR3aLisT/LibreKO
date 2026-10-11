using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class DurationTextTests
{
    [Theory]
    [InlineData(3 * 86400 + 5 * 3600 + 120, "3d 5h")]
    [InlineData(2 * 3600 + 30 * 60, "2h 30m")]
    [InlineData(45 * 60, "45m")]
    [InlineData(20, "1m")]
    [InlineData(0, "")]
    public void TimeLeftReadsInItsTwoLargestUnits(long seconds, string text)
    {
        Assert.Equal(text, DurationText.Short(seconds));
    }
}
