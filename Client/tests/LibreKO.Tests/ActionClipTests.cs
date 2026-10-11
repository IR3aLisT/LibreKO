using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class ActionClipTests
{
    [Fact]
    public void AMovingClipLastsItsBlendHoldPlusItsLength()
    {
        Assert.Equal(1.35, ActionClip.Duration(1.2, 0.15), 6);
        Assert.Equal(1.2, ActionClip.Duration(1.2, 0), 6);
    }

    [Fact]
    public void AStaticPoseKeepsItsOwnHold()
    {
        Assert.Equal(ActionClip.Hold(0.001, 0.15), ActionClip.Duration(0.001, 0.15), 6);
    }
}
