using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class FloaterNumbersTests
{
    [Theory]
    [InlineData(950, "950")]
    [InlineData(9_999, "9,999")]
    [InlineData(10_000, "10K")]
    [InlineData(12_345, "12.3K")]
    [InlineData(99_999, "99.9K")]
    [InlineData(123_456, "123K")]
    [InlineData(1_250_000, "1.2M")]
    [InlineData(2_000_000_000, "2B")]
    [InlineData(-45_678, "-45.6K")]
    public void LargeNumbersAbbreviateWithOneFlooredDecimalUnderAHundred(long amount, string expected)
    {
        Assert.Equal(expected, FloaterNumbers.Format(amount));
    }

    [Fact]
    public void HitsWithinATenthAndAHalfOfASecondMerge()
    {
        Assert.True(FloaterNumbers.Merges(10.0, 10.1));
        Assert.False(FloaterNumbers.Merges(10.0, 10.2));
    }
}
