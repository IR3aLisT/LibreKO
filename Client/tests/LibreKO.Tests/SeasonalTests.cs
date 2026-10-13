using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class SeasonalTests
{
    [Theory]
    [InlineData(Seasonal.Santa, Seasonal.SantaFx)]
    [InlineData(Seasonal.Angel, Seasonal.AngelFx)]
    [InlineData(0, null)]
    [InlineData(3, null)]
    public void EachStatePlaysItsEffectOnce(int state, int? fx) => Assert.Equal(fx, Seasonal.FxFor(state));
}
