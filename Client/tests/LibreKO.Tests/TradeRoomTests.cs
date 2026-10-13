using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class TradeRoomTests
{
    [Theory]
    [InlineData(3, 3, 900, 1000, true)]
    [InlineData(2, 3, 900, 1000, false)]
    [InlineData(5, 1, 1100, 1000, false)]
    [InlineData(5, 1, 1100, 0, true)]
    public void IncomingItemsNeedBothRoomAndWeight(int free, int incoming, long weightAfter, long maxWeight, bool fits)
    {
        Assert.Equal(fits, new TradeRoom(free, incoming, weightAfter, maxWeight).Fits);
    }
}
