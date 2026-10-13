using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class RankSymbolTests
{
    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    [InlineData(4, 1)]
    [InlineData(5, 2)]
    [InlineData(10, 2)]
    [InlineData(11, 3)]
    [InlineData(40, 3)]
    [InlineData(41, 4)]
    [InlineData(100, 4)]
    [InlineData(101, 5)]
    [InlineData(200, 5)]
    public void PlacesFallIntoTheSixTiers(int rank, int tier)
    {
        Assert.Equal(tier, RankSymbol.TierOf(rank));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(201)]
    [InlineData(NationRankAura.Unranked)]
    public void UnrankedPlayersHaveNoSymbol(int rank)
    {
        Assert.Equal(RankSymbol.NoTier, RankSymbol.TierOf(rank));
        Assert.Null(RankSymbol.TexturePath(rank));
    }

    [Theory]
    [InlineData(3, 7, "knights_1")]
    [InlineData(7, 3, "personal_1")]
    [InlineData(NationRankAura.Unranked, 1, "personal_0")]
    [InlineData(150, NationRankAura.Unranked, "knights_5")]
    [InlineData(0, 12, "personal_3")]
    public void ThePlateShowsTheBetterOfTheTwoPlaces(int knights, int personal, string symbol)
    {
        Assert.EndsWith($"{symbol}.png", RankSymbol.PlatePath(knights, personal));
    }

    [Fact]
    public void NoPlaceShowsNoPlateSymbol()
    {
        Assert.Null(RankSymbol.PlatePath(NationRankAura.Unranked, NationRankAura.Unranked));
    }
}
