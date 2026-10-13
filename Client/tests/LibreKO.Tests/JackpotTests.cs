using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class JackpotTests
{
    [Theory]
    [InlineData(2, 30727, 30905)]
    [InlineData(50, 30725, 30903)]
    [InlineData(1000, 30722, 30900)]
    [InlineData(3, 0, 0)]
    public void EachMultiplierHasItsEffect(int times, int exp, int coins)
    {
        Assert.Equal(exp, Jackpot.ExpEffect(times));
        Assert.Equal(coins, Jackpot.CoinEffect(times));
    }
}
