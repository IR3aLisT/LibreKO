using Xunit;
using LibreKO;

namespace LibreKO.Tests;

public class BridgeTests
{
    [Fact]
    public void TheLoweredBridgeTiltsMinus110DegreesOnTheMirroredModel()
    {
        Assert.Equal(-110.0f, World.BridgeLoweredPitch);
    }
}
