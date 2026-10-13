using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class SeekingPartyTests
{
    [Theory]
    [InlineData(5, 1, 13)]
    [InlineData(40, 26, 60)]
    [InlineData(70, 46, 83)]
    public void TheBandFollowsTheRetailFormula(int level, int min, int max) =>
        Assert.Equal((min, max), SeekingParty.Band(level));

    [Fact]
    public void EachStateShowsItsOwnLine()
    {
        static string Text(int id, string fallback) => fallback;

        Assert.Equal("Seeking Party : Level 26 ~ 60", SeekingParty.Line(SeekingParty.SeekingBand, 40, Text));
        Assert.Equal("Seeking_Party", SeekingParty.Line(SeekingParty.Seeking, 40, Text));
        Assert.Null(SeekingParty.Line(SeekingParty.NotSeeking, 40, Text));
    }
}
