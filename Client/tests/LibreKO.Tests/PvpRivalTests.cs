using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class PvpRivalTests
{
    [Theory]
    [InlineData(71, true)]
    [InlineData(72, true)]
    [InlineData(73, true)]
    [InlineData(61, true)]
    [InlineData(64, true)]
    [InlineData(66, true)]
    [InlineData(65, false)]
    [InlineData(21, false)]
    public void TheRivalIsMarkedOnlyInThePvpZones(int zone, bool marked) =>
        Assert.Equal(marked, PvpRival.MarksIn(zone));
}
